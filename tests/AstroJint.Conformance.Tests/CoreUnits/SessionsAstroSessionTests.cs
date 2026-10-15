using Microsoft.Extensions.Logging;
using static AstroJint.Conformance.CoreUnits.AppSessions;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/sessions/astro-session.test.ts</c>, which drives the <c>AstroSession</c> class directly
/// with a mock cookie jar and a mock unstorage. 34 of the 35 tests are adapted and one is not applicable; none had to be skipped.
/// <para>
/// The class cannot be reached from outside Astro, so the tests drive <c>Astro.session</c>, which is the same class, in the
/// built <c>app-sessions</c> fixture. The endpoint <c>/api/ops</c> makes the calls of a test (<c>set</c>, <c>get</c>, <c>has</c>,
/// <c>delete</c>, <c>keys</c>, <c>regenerate</c>, <c>destroy</c>, <c>sessionID</c>; see <c>SessionOp</c>) on the session of one request,
/// in order, and reports what each returned or the message it threw. The mocks become real things of the host: the cookie jar
/// is the <c>Cookie</c> request header and the <c>Set-Cookie</c> response headers, and the unstorage is the
/// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> of the host (the session store of the adapter keeps the
/// data there), here a <c>SessionSpyCache</c> that a test seeds with the data of a session, reads back and asks for the calls the
/// host made. The data that upstream builds with <c>devalue.stringify(new Map(...))</c> is written out by hand in the same format
/// (<c>SessionSeeds</c>).
/// </para>
/// <para>
/// Differences that apply to many tests. (1) <c>createSession(...)</c> with <c>defaultConfig</c> is a build of the fixture with
/// <c>session: { cookie: 'test-session', ttl: 60 }</c>; the driver is the one that the adapter wires, not <c>memory</c>.
/// (2) The mock cookie jar of upstream always holds the session id <c>00000000-0000-4000-8000-000000000000</c>; a request here
/// carries a version 4 UUID of its own (every test uses another one, because the spy is shared by the tests of the class) and,
/// where upstream's storage mock answers with data, the spy is seeded under that id. (3) A cookie id for which the storage has
/// no data is replaced by Astro with a new id when the session is loaded, so a test that starts without data and later needs
/// the id (to see what was written, or to make a second request) takes it from the <c>Set-Cookie</c> header of the response, which
/// is where the client would take it from. (4) Calls of upstream's <c>session[PERSIST_SYMBOL]()</c> ("simulate end of request") are
/// the end of the request: Astro persists the session when the response is complete, so the cache is read after the request.
/// (5) Upstream's assertions on what the cookie mock received (<c>cookieSet</c>, <c>cookieName</c>, <c>cookieOptions</c>) are
/// assertions on the <c>Set-Cookie</c> header: its name, <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Lax</c>, <c>Path=/</c>, or for a
/// deleted cookie <c>deleted</c> and the date of 1970.
/// </para>
/// <para>
/// Per test. "should set cookie on first value set" also asserts that a request that only reads sets no cookie. "should set secure
/// to false in development" is not applicable: the development mode exists only in <c>astro dev</c>, and the host runs the built
/// site, which is always the production mode (the production test next to it covers the cookie of the built site). "should not
/// initialize the storage driver or read when no session cookie is present" cannot observe the driver factory, so it asserts the
/// consequence that matters: the request makes no call on the cache. "should handle storage setItem failures" cannot see a rejected
/// <c>session[PERSIST_SYMBOL]()</c>: the failure of the write at the end of the request makes the request answer 500, and the host
/// logs "The distributed cache failed to write session data." with the exception. "should route errors to logger and reset #partial
/// flag" reads the logger from the log of the host (an Astro error line of the console) and counts the reads of the spy instead of
/// those of a mock. The tests of "should persist data after regeneration without a subsequent set()" and "should maintain deletion
/// after persistence" use two or three requests where upstream uses two sessions over one storage.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/sessions/astro-session.test.ts")]
public class SessionsAstroSessionTests(SiteHost sites)
{
    private const string Variant = "sessions-astro-session";
    private const string CookieName = "test-session";
    private const string UuidPattern = "(?i)^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";

    // The configuration of upstream's `defaultConfig` and the three variations of it that the tests of the cookie make.
    private static readonly object TestSession = new { session = new { cookie = CookieName, ttl = 60 } };
    private static readonly object CustomCookieName = new { session = new { cookie = "custom-session", ttl = 60 } };
    private static readonly object CookieThatIsNotHttpOnly = new { session = new { cookie = new { httpOnly = false }, ttl = 60 } };
    private static readonly object NoCookieName = new { session = new { ttl = 60 } };

    private Task<(ConformanceSite Site, SessionSpyCache Cache)> SiteAsync(object? config = null) =>
        sites.GetSessionSiteAsync(Variant, config ?? TestSession);

    // #region Basic Operations

    [Fact(DisplayName = "should set and get a value")]
    public async Task Session_SetThenGet_ReturnsTheValue()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("user", new { id = 1, name = "Test User" }), SessionOp.Get("user"));

        Assert.Equal("""{"id":1,"name":"Test User"}""", run.Step(1).Json);
    }

    [Fact(DisplayName = "should check if value exists")]
    public async Task Session_Has_ReportsWhetherTheKeyIsSet()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"), SessionOp.Has("key"), SessionOp.Has("nonexistent"));

        Assert.Equal("true", run.Step(1).Json);
        Assert.Equal("false", run.Step(2).Json);
    }

    [Fact(DisplayName = "should delete a value")]
    public async Task Session_Delete_RemovesTheValue()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"), SessionOp.Delete("key"), SessionOp.Get("key"));

        Assert.True(run.Step(2).IsUndefined);
    }

    [Fact(DisplayName = "should list all keys")]
    public async Task Session_Keys_ListsEveryKey()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key1", "value1"), SessionOp.Set("key2", "value2"), SessionOp.Keys());

        Assert.Equal("""["key1","key2"]""", run.Step(2).Json);
    }

    // #endregion

    // #region Cookie Management

    [Fact(DisplayName = "should set cookie on first value set")]
    public async Task Session_FirstSet_SetsTheCookie()
    {
        var (site, _) = await SiteAsync();

        var read = await site.RunSessionAsync(null, SessionOp.Get("key"));
        var write = await site.RunSessionAsync(null, SessionOp.Set("key", "value"));

        Assert.True(read.Step(0).IsUndefined);
        Assert.Empty(read.SetCookies);
        write.Step(0);
        Assert.Matches(UuidPattern, write.CookieValue(CookieName));
    }

    [Fact(DisplayName = "should delete cookie on destroy")]
    public async Task Session_Destroy_DeletesTheCookie()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(CookieFor(CookieName, NewId()), SessionOp.Destroy());

        run.Step(0);
        Assert.Equal("deleted", run.CookieValue(CookieName));
        Assert.Contains("Path=/", run.CookieAttributes(CookieName));
        Assert.Contains("Expires=Thu, 01 Jan 1970 00:00:00 GMT", run.CookieAttributes(CookieName));
    }

    // #endregion

    // #region Session Regeneration

    [Fact(DisplayName = "should preserve data when regenerating session")]
    public async Task Session_Regenerate_KeepsTheData()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"), SessionOp.Regenerate(), SessionOp.Get("key"));

        Assert.Equal("\"value\"", run.Step(2).Json);
    }

    [Fact(DisplayName = "should generate new session ID on regeneration")]
    public async Task Session_Regenerate_GeneratesANewId()
    {
        var (site, _) = await SiteAsync();
        var cookieId = NewId();

        var run = await site.RunSessionAsync(CookieFor(CookieName, cookieId), SessionOp.Id(), SessionOp.Regenerate(), SessionOp.Id());

        // The session has no id of its own before the first change, which is what upstream's `initialId` is.
        Assert.True(run.Step(0).IsUndefined);
        var newId = run.Step(2).Text;
        Assert.Matches(UuidPattern, newId);
        Assert.NotEqual(cookieId, newId);
        Assert.Equal(newId, run.CookieValue(CookieName));
    }

    [Fact(DisplayName = "should persist data after regeneration without a subsequent set()")]
    public async Task Session_Regenerate_PersistsTheDataUnderTheNewId()
    {
        var (site, cache) = await SiteAsync();

        var first = await site.RunSessionAsync(CookieFor(CookieName, NewId()), SessionOp.Set("user", "alice"));
        var oldId = first.CookieValue(CookieName);
        Assert.NotNull(oldId);
        Assert.Contains("alice", cache.Stored(oldId), StringComparison.Ordinal);

        var regenerated = await site.RunSessionAsync(CookieFor(CookieName, oldId), SessionOp.Regenerate());
        regenerated.Step(0);
        var newId = regenerated.CookieValue(CookieName);
        Assert.NotNull(newId);
        Assert.NotEqual(oldId, newId);
        Assert.Null(cache.Stored(oldId));

        // The next request is upstream's "new session that reads from the same storage".
        var next = await site.RunSessionAsync(CookieFor(CookieName, newId), SessionOp.Get("user"));
        Assert.Equal("\"alice\"", next.Step(0).Json);
    }

    // #endregion

    // #region Data Persistence

    [Fact(DisplayName = "should persist data to storage")]
    public async Task Session_Set_PersistsTheValueAtTheEndOfTheRequest()
    {
        var (site, cache) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"));

        run.Step(0);
        var id = run.CookieValue(CookieName);
        Assert.NotNull(id);
        Assert.Contains("value", cache.Stored(id), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should load data from storage")]
    public async Task Session_Get_LoadsTheDataFromTheStorage()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Value);

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("key"));

        Assert.Equal("\"value\"", run.Step(0).Json);
    }

    [Fact(DisplayName = "should remove expired session data")]
    public async Task Session_Get_IgnoresExpiredData()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Expired);

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("key"));

        Assert.True(run.Step(0).IsUndefined);
    }

    // #endregion

    // #region Error Handling

    [Fact(DisplayName = "should throw error when setting invalid data")]
    public async Task Session_SetWithAFunction_Throws()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.SetFunction("key"));

        Assert.Matches("could not be serialized", run.Error(0));
    }

    [Fact(DisplayName = "should throw error when setting empty key")]
    public async Task Session_SetWithAnEmptyKey_Throws()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("", "value"));

        Assert.Matches("key was not provided", run.Error(0));
    }

    [Fact(DisplayName = "should handle corrupted storage data")]
    public async Task Session_GetWithCorruptedData_Throws()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Corrupt);

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("key"));

        Assert.Matches("could not be parsed", run.Error(0));
    }

    // #endregion

    // #region Configuration

    [Fact(DisplayName = "should use custom cookie name from config")]
    public async Task Session_Set_UsesTheConfiguredCookieName()
    {
        var (site, _) = await SiteAsync(CustomCookieName);

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"));

        run.Step(0);
        Assert.Matches(UuidPattern, run.CookieValue("custom-session"));
        Assert.Null(run.CookieValue(CookieName));
    }

    [Fact(DisplayName = "should use default cookie name if not specified")]
    public async Task Session_Set_UsesTheDefaultCookieName()
    {
        var (site, _) = await SiteAsync(NoCookieName);

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"));

        run.Step(0);
        Assert.Matches(UuidPattern, run.CookieValue("astro-session"));
    }

    // #endregion

    // #region Sparse Data Operations

    [Fact(DisplayName = "should handle multiple operations in sparse mode")]
    public async Task Session_MixedOperations_AffectOnlyTheirKeys()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Sparse);

        var run = await site.RunSessionAsync(
            CookieFor(CookieName, id),
            SessionOp.Delete("delete"),
            SessionOp.Set("update", "new"),
            SessionOp.Set("new", "value"),
            SessionOp.Get("keep"),
            SessionOp.Get("delete"),
            SessionOp.Get("update"),
            SessionOp.Get("new"));

        Assert.Equal("\"original\"", run.Step(3).Json);
        Assert.True(run.Step(4).IsUndefined);
        Assert.Equal("\"new\"", run.Step(5).Json);
        Assert.Equal("\"value\"", run.Step(6).Json);
    }

    [Fact(DisplayName = "should persist deleted state across multiple operations")]
    public async Task Session_Delete_StaysDeletedAfterOtherOperations()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.RawValue);

        var run = await site.RunSessionAsync(
            CookieFor(CookieName, id),
            SessionOp.Delete("key"),
            SessionOp.Get("key"),
            SessionOp.Has("key"),
            SessionOp.Set("other", "value"),
            SessionOp.Get("key"));

        Assert.True(run.Step(1).IsUndefined);
        Assert.Equal("false", run.Step(2).Json);
        Assert.True(run.Step(4).IsUndefined);
    }

    [Fact(DisplayName = "should maintain deletion after persistence")]
    public async Task Session_Delete_IsKeptByTheNextRequest()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.RawValue);

        var deletion = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Delete("key"));
        deletion.Step(0);
        var stored = cache.Stored(id);
        Assert.NotNull(stored);
        Assert.DoesNotContain("value", stored, StringComparison.Ordinal);

        var next = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("key"));
        Assert.True(next.Step(0).IsUndefined);
    }

    [Fact(DisplayName = "should persist delete as the first mutation (no prior get/set)")]
    public async Task Session_DeleteAsTheFirstCall_IsPersisted()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Token);

        var deletion = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Delete("token"));
        deletion.Step(0);

        var next = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("token"));
        Assert.True(next.Step(0).IsUndefined);
    }

    [Fact(DisplayName = "should update existing values in sparse mode")]
    public async Task Session_Set_ReplacesAnExistingValue()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.RawOld);

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Set("key", "new"), SessionOp.Get("key"), SessionOp.Keys());

        Assert.Equal("\"new\"", run.Step(1).Json);
        Assert.Equal("""["key"]""", run.Step(2).Json);
    }

    // #endregion

    // #region Cleanup Operations

    [Fact(DisplayName = "should clean up destroyed sessions on persist")]
    public async Task Session_SetThenDestroy_RemovesTheSessionFromTheStorage()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Value);
        var mark = cache.Mark();

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Set("key", "value"), SessionOp.Id(), SessionOp.Destroy());

        Assert.Equal(id, run.Step(1).Text);
        run.Step(2);
        Assert.Contains(new SessionCall(SessionCallKind.Remove, id), cache.CallsSince(mark));
        Assert.Null(cache.Stored(id));
    }

    [Fact(DisplayName = "should destroy sessions that haven't been loaded")]
    public async Task Session_DestroyWithoutLoading_RemovesTheSessionFromTheStorage()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.Value);
        var mark = cache.Mark();

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Destroy());

        run.Step(0);
        Assert.Equal(new SessionCall(SessionCallKind.Remove, id), Assert.Single(cache.CallsSince(mark)));
    }

    [Fact(DisplayName = "should not destroy a non-UUID session cookie")]
    public async Task Session_DestroyWithANonUuidCookie_LeavesTheStorageAlone()
    {
        var (site, cache) = await SiteAsync();
        var mark = cache.Mark();

        var run = await site.RunSessionAsync(CookieFor(CookieName, "unrelated-storage-key"), SessionOp.Destroy());

        run.Step(0);
        Assert.Empty(cache.CallsSince(mark));
    }

    // #endregion

    // #region Cookie Security

    [Fact(DisplayName = "should enforce httpOnly cookie setting")]
    public async Task Session_Set_AlwaysSetsAnHttpOnlyCookie()
    {
        var (site, _) = await SiteAsync(CookieThatIsNotHttpOnly);

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"));

        run.Step(0);
        Assert.Contains("HttpOnly", run.CookieAttributes("astro-session"));
    }

    [Fact(DisplayName = "should set secure and sameSite by default in production")]
    public async Task Session_Set_SetsASecureLaxCookie()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "value"));

        run.Step(0);
        var attributes = run.CookieAttributes(CookieName);
        Assert.Contains("Secure", attributes);
        Assert.Contains("SameSite=Lax", attributes);
    }

    [Fact(DisplayName = "should reject a non-UUID session cookie value and generate a new ID")]
    public async Task Session_Set_ReplacesACookieThatIsNotAUuid()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(CookieFor(CookieName, "not-a-uuid-at-all"), SessionOp.Set("key", "value"), SessionOp.Id());

        var id = run.Step(1).Text;
        Assert.NotNull(id);
        Assert.NotEqual("not-a-uuid-at-all", id);
        Assert.Matches(UuidPattern, id);
        Assert.Equal(id, run.CookieValue(CookieName));
    }

    // #endregion

    // #region Storage Errors

    [Fact(DisplayName = "should handle storage setItem failures")]
    public async Task Session_Persist_FailsTheRequestWhenTheStorageCannotWrite()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.EmptyMap);
        cache.FailWrites(id, "Storage full");

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Set("key", "value"));

        Assert.Equal(500, run.Status);
        Assert.Contains(
            site.Logs,
            entry => entry.Level == LogLevel.Error
                && entry.Message == "The distributed cache failed to write session data."
                && entry.Exception?.Message == "Storage full");
    }

    [Fact(DisplayName = "should handle invalid Map data")]
    public async Task Session_GetWithDataThatIsNotAMap_Throws()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.NotAMap);

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("key"));

        Assert.Matches("The session data was an invalid type", run.Error(0));
    }

    // #endregion

    // #region No-Cookie Short Circuit

    [Fact(DisplayName = "should not initialize the storage driver or read when no session cookie is present")]
    public async Task Session_GetWithoutACookie_DoesNotTouchTheStorage()
    {
        var (site, cache) = await SiteAsync();
        var mark = cache.Mark();

        var run = await site.RunSessionAsync(null, SessionOp.Get("nonexistent"));

        Assert.True(run.Step(0).IsUndefined);
        Assert.Empty(cache.CallsSince(mark));
    }

    [Fact(DisplayName = "should read from storage when session cookie is present")]
    public async Task Session_GetWithACookie_ReadsTheStorageOnce()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        cache.Seed(id, SessionSeeds.User);
        var mark = cache.Mark();

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Get("user"));

        Assert.Equal("\"alice\"", run.Step(0).Json);
        Assert.Equal(new SessionCall(SessionCallKind.Get, id), Assert.Single(cache.CallsSince(mark)));
    }

    [Fact(DisplayName = "should allow set() then get() without a cookie (new session)")]
    public async Task Session_SetThenGetWithoutACookie_ReturnsTheValue()
    {
        var (site, _) = await SiteAsync();

        var run = await site.RunSessionAsync(null, SessionOp.Set("key", "expected"), SessionOp.Get("key"));

        Assert.Equal("\"expected\"", run.Step(1).Json);
        Assert.Matches(UuidPattern, run.CookieValue(CookieName));
    }

    // #endregion

    // #region Error Recovery

    [Fact(DisplayName = "should route errors to logger and reset #partial flag")]
    public async Task Session_RegenerateWithCorruptData_LogsTheErrorAndDoesNotReadAgain()
    {
        var (site, cache) = await SiteAsync();
        var id = NewId();
        // A string that devalue turns into an array, not a Map, so loading the session throws.
        cache.Seed(id, SessionSeeds.NumberArray);
        var mark = cache.Mark();

        var run = await site.RunSessionAsync(CookieFor(CookieName, id), SessionOp.Regenerate(), SessionOp.Get("foo"));

        run.Step(0);
        Assert.True(run.Step(1).IsUndefined);
        Assert.Contains(
            site.ConsoleEntries(),
            entry => entry.Level == LogLevel.Error
                && entry.Message.Contains("Failed to load session data during regeneration", StringComparison.Ordinal));
        var reads = cache.CallsSince(mark).Where(call => call.Kind == SessionCallKind.Get).ToList();
        Assert.Equal(new SessionCall(SessionCallKind.Get, id), Assert.Single(reads));
    }

    // #endregion
}
