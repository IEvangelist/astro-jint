/// <reference types="astro/client" />

declare namespace App {
  /** Set by the ASP.NET Core host (`AstroOptions.LocalsFactory`) for every on-demand request. */
  interface Locals {
    traceId: string;
    user: string | null;
  }
}
