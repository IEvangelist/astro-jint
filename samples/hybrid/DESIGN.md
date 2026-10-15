---
version: alpha
name: Hybrid Astro on Jint (Nautical Chart)
description: A flat, printed-chart interface for a hybrid Astro site. Land paper marks pages generated at build time, water marks pages rendered on demand, and a single magenta accent marks everything you can act on.
colors:
  primary: "#af0f6f"
  on-primary: "#fcfcf9"
  primary-hover: "#9c1c68"
  chart-ink: "#142237"
  chart-ink-soft: "#455369"
  land-paper: "#f5f5f0"
  shallow-water: "#d5eefc"
  open-sea: "#badff6"
  sheet-vellum: "#fdfcfa"
  error: "#b71a18"
typography:
  display:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 5.25rem
    fontWeight: 800
    lineHeight: 1.02
    letterSpacing: "-0.03em"
  headline:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 4.25rem
    fontWeight: 800
    lineHeight: 1.05
    letterSpacing: "-0.03em"
  title:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 2.125rem
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.02em"
  row-title:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 1.75rem
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-0.02em"
  lead:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 1.375rem
    fontWeight: 400
    lineHeight: 1.5
  reading:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 1.125rem
    fontWeight: 400
    lineHeight: 1.7
  body:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 1rem
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 0.9375rem
    fontWeight: 400
  nav:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 0.9rem
    fontWeight: 500
  control:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 1rem
    fontWeight: 700
    lineHeight: 1.2
  live-value:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 1.125rem
    fontWeight: 700
    fontFeature: '"tnum"'
  figure:
    fontFamily: "Archivo, ui-sans-serif, system-ui, 'Segoe UI', sans-serif"
    fontSize: 5rem
    fontWeight: 800
    lineHeight: 1
    letterSpacing: "-0.02em"
    fontFeature: '"tnum"'
rounded:
  sm: 2px
spacing:
  space-1: 0.25rem
  space-2: 0.5rem
  space-3: 0.75rem
  space-4: 1rem
  space-5: 1.5rem
  space-6: 2rem
  space-7: 3rem
  space-8: 4.5rem
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-primary}"
    typography: "{typography.control}"
    rounded: "{rounded.sm}"
    padding: "0.625rem 1.25rem"
    height: 2.75rem
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
    textColor: "{colors.on-primary}"
  button-quiet:
    textColor: "{colors.chart-ink}"
    typography: "{typography.control}"
    rounded: "{rounded.sm}"
    padding: "0.625rem 1.25rem"
    height: 2.75rem
  button-disabled:
    textColor: "{colors.chart-ink-soft}"
    typography: "{typography.control}"
    rounded: "{rounded.sm}"
  nav-link:
    textColor: "{colors.chart-ink}"
    typography: "{typography.nav}"
    height: 2.75rem
  plate:
    backgroundColor: "{colors.sheet-vellum}"
    textColor: "{colors.chart-ink}"
    padding: 2rem
  plate-water:
    backgroundColor: "{colors.shallow-water}"
    textColor: "{colors.chart-ink}"
    padding: 2rem
  fix-plate:
    backgroundColor: "{colors.open-sea}"
    textColor: "{colors.chart-ink}"
  input:
    backgroundColor: "{colors.sheet-vellum}"
    textColor: "{colors.chart-ink}"
    rounded: "{rounded.sm}"
    padding: "0.625rem 0.75rem"
    height: 2.75rem
  skip-link:
    backgroundColor: "{colors.chart-ink}"
    textColor: "{colors.land-paper}"
    typography: "{typography.control}"
    padding: "0.75rem 1rem"
---

# Design System: Hybrid Astro on Jint (Nautical Chart)

## Overview

**Creative North Star: "The Navigator's Fix"**

A hybrid site does two kinds of work. Some pages are printed ahead of time, at build. Others are taken fresh on every request. The interface treats that split the way a navigator treats a fix. The printed almanac is the land: upright type on warm neutral paper, marked with a solid dot. The sight taken right now is the water: a pale blue ground, live values in bold italic, marked with a hollow ring. A visitor can tell which kind of page they are on from the ground color and the posture of the type alone, before reading the words that confirm it.

The structure is drawn rather than boxed. Panels are plates with a double-line neatline. A dashed scale bar closes the header and opens the footer. A faint graticule of hairline grid lines sits behind the top of every page and fades out well before the footer. The compass star is both the logo and the favicon, and its magenta north needle is the one place the accent works as a mark instead of as an action.

This system rejects the SaaS-card look: no shadows, no soft rounded cards, no gradient or glass surfaces. The nautical idea supplies structure, not costume. There are no ropes, anchors, parchment textures, or chart lettering. The only drawn picture is the coastline inside the hero fix plate, and the only large ornament is the compass rose on the 404 and 500 pages. No photography, stock imagery, or icon sets appear anywhere.

**Key Characteristics:**
- Two grounds carry meaning: neutral land paper for static pages, pale blue water for on-demand pages.
- Flat everywhere. Depth comes from tint steps and ink lines, never from shadow.
- One accent, magenta, for actions, links, focus, and the compass needle.
- One 2px corner radius for controls and true circles for markers. Panels are square.
- One heavy grotesque typeface. Live, server-produced values are bold italic with tabular numerals.
- Light and dark are both native. The dark scheme is a night chart, not an inversion.
- Every interactive target is at least 44px tall, and navigation collapses into a popover menu below 70rem.

## Colors

The palette is a printed chart: deep navy ink on warm neutral paper, two blues for water, and one magenta accent. It is restrained on purpose, so the two grounds can carry meaning.

### Primary
- **Fix Magenta** (`primary`): The only accent. It fills the main button, colors links (underlined at 1.5px), draws the 2px focus ring, underlines a hovered row title, forms the north needle of the compass star, and washes selected text at 28% strength.
- **Magenta Text** (`on-primary`): Text and icons that sit on a Fix Magenta fill. It is a warm off-white, never pure white.
- **Deep Magenta** (`primary-hover`): The button fill and border on hover, made by mixing 12% ink into Fix Magenta.

### Neutral
- **Chart Ink** (`chart-ink`): Deep navy for all text, the neatline, the scale bar, and the hairlines (22% mix) and graticule (15% mix) derived from it. It holds at least 10:1 contrast on every ground.
- **Chart Ink Soft** (`chart-ink-soft`): Secondary text: captions, the footer, readout terms, and the disabled state. Its lowest pairing is about 4.9:1, on the deep clock band.
- **Land Paper** (`land-paper`): The ground of every page generated at build time, and the default page background.
- **Shallow Water** (`shallow-water`): The ground of every page rendered on demand, and the fill of the on-demand route plate.
- **Sheet Vellum** (`sheet-vellum`): The lightest step. Plates, inputs, the mobile menu sheet, and the land half of the fix plate.
- **Open Sea** (`open-sea`): The deeper blue of the fix plate's sea and the thank-you plate. The band that holds the live clock is derived from it and is always one step darker.

### Error
- **Hazard Red** (`error`): The 2px border on an invalid field and the text of its message. It always travels with a triangle glyph and a 600 weight, so color is never the only signal.

### Night Chart
The dark scheme is drawn as a night chart with its own values, not a color inversion: land #12161c, water #092032, sheet #1c242c, open sea #0a3149, ink #e5ecf1, soft ink #a8b6c3, magenta #f886bb with dark text #201017 on it (hover #f793c1), and error #f89a8e. Every text pairing clears 4.5:1 in both schemes. Magenta as text holds about 5.5:1 or better on land, water, and sheet, and text on the magenta fill holds 6.5:1 or better.

### Named Rules
**The Land and Water Rule.** Ground color states the rendering mode. Land paper means generated at build, shallow water means rendered on demand. Never put water on a static page, never put land on an on-demand page, and never use either as decoration.

**The One Accent Rule.** Magenta is the only accent. It marks what can be acted on (buttons, links, focus) and the compass needle. It never floods a panel, and a second accent is never introduced.

## Typography

**Display Font:** Archivo (variable weight axis, upright and italic, with ui-sans-serif, system-ui, Segoe UI, and sans-serif as fallbacks)
**Body Font:** Archivo (the same family)
**Label/Mono Font:** Archivo for labels. The system monospace stack appears only for inline code, at 0.9em on a faint ink wash.

**Character:** A heavy, neutral grotesque. Headlines sit at the heaviest weight with tight tracking, so they read like plate titles rather than marketing copy. Body text is a plain 400 at generous line height. There is no chart lettering, serif, or script: the nautical idea lives in the structure, not the typeface. Italic is not decoration. It is the signal for live data.

### Hierarchy
The large sizes are fluid in the shipped stylesheet and scale with the viewport between the two bounds given below. Wherever a value is fluid (these font sizes, and the padding of a plate), the frontmatter carries the upper bound, because the token format takes a single dimension.

- **Display** (800, 44px to 84px fluid, line height 1.02, tracking -0.03em): The home hero headline only.
- **Headline** (800, 40px to 68px fluid, 1.05, -0.03em, 20 characters wide at most): The h1 of every inner page.
- **Title** (700, 24px to 34px fluid, 1.1, -0.02em): Section headings.
- **Row Title** (700, 22px to 28px fluid, 1.2, -0.02em): The title of each row in a list of posts or products.
- **Lead** (400, 18px to 22px fluid, 1.5, 48 characters wide at most): The sentence under a headline.
- **Reading** (400, 18px, 1.7, 62 characters wide at most): Article and long-form prose.
- **Body** (400, 16px, 1.6): Everything else, wrapped for even line endings.
- **Label** (400, 15px, with 600 for readout terms): The footer, captions, and readout terms.
- **Nav** (500, 14.4px, with 700 for the current page): Header links. They grow to 16px inside the mobile menu sheet.
- **Control** (700, 16px, 1.2): Buttons, the menu button, and field labels.
- **Live Value** (700 italic, tabular numerals, 18px): Readout values and server answers. The clock time in the fix plate is the same style at 22px.
- **Figure** (800 italic, tabular numerals, 48px to 80px fluid, line height 1, -0.02em): The one large number on a page, such as a product price. The session tally is the same style one notch smaller.

### Named Rules
**The Live Italic Rule.** Anything the server produced for this request, and any label that says on demand, is set in bold italic with tabular numerals. Static copy stays upright. Italic is a data signal, so it is not used for emphasis in running text.

**The Weight Is the Hierarchy Rule.** One typeface and three weights (800, 700, 400) carry the whole hierarchy, with tracking that only ever tightens. No second family, no uppercase letterspaced eyebrow labels.

## Layout

One centered container, 76rem wide (about 1216px), with a fluid gutter that runs from 16px on phones to 40px on large screens. The hero splits 7fr to 5fr from 56rem: headline, lead, and actions on the left, the fix plate on the right. Other two-column pairings (copy beside a plate or a list) switch on at 48rem or 56rem, in 7fr to 5fr or 5fr to 7fr proportions, and stack to a single column below that.

The header is sticky and 64px tall, painted on the page ground so it carries the mode like the rest of the page. The brand sits on the left. From 70rem (about 1120px) the navigation runs inline in two groups, static pages first and on-demand pages second, divided by a hairline. Below 70rem a Menu button opens a popover sheet. A dashed scale bar sits directly under the header and again directly above the footer. The footer is a wrapping row, spaced apart, holding the mode badge, the prototype note, and the source link.

Spacing runs on a 4px base with eight steps (4, 8, 12, 16, 24, 32, 48, 72px). Section blocks are separated by 48px to 80px, plates are padded 20px to 32px, list rows are padded 24px, and form fields sit 24px apart. Measure is capped everywhere: headlines at 20 characters, leads at 48, prose at 62, and product descriptions at 40.

Every inner page opens the same way: the headline, then a one-line mode statement with its marker, then the content.

### Named Rules
**The Graticule Rule.** Every page carries the same faint grid behind its top 512px, in 4rem squares of 1px lines at 15% ink, masked so it fades out well before the footer. It is the only decoration that runs behind content, and it never gets stronger than 15% ink.

**The 44 Rule.** Buttons, inputs, navigation links, and menu rows are at least 44px tall. The mobile menu sheet rows are 48px.

## Elevation & Depth

This system has no shadows. There is no box shadow, drop shadow, text shadow, blur, or filter anywhere in the stylesheet. Depth is a printed idea, made from tint steps and lines.

- **Tint steps:** land paper for the page, sheet vellum for plates and inputs, shallow water for on-demand ground, open sea for the deepest plates.
- **Ink rules:** 1px hairlines at 22% ink between rows, and a 2px full-ink rule at the top of a list and as the divider between the two navigation groups in the mobile sheet.
- **Neatline:** every plate has a 1px ink border and a second 1px ink line set 4px inside it.
- **Deeper is darker:** the band where the live clock sits is a step darker than the sea above it, in both schemes.
- **Header:** opaque on the page ground. The scale bar beneath it is the separator, not a shadow.

### Named Rules
**The Flat Chart Rule.** No shadows, glows, blurs, translucent glass, or decorative gradients. The only gradients in the system draw lines: the graticule and the scale bar.

**The Deeper Is Darker Rule.** Water gets darker as it gets deeper. In the dark scheme the deep band mixes toward the land color rather than toward the now-light ink, so it still reads as deeper than the shallows.

## Shapes

One corner radius, 2px, used on buttons, inputs, the menu button, and inline code. Plates, rows, the menu sheet, and the header are square. Nothing is a pill, and nothing is a rounded card. True circles are reserved for markers: the 10px mode marker, the 8px navigation markers, and the 28px numbered bullets of the request path. Chevrons are drawn from two rotated borders, not from icons, and the error glyph is a triangle clipped from a box.

The compass star is a 1px ring around an eight-point star in ink, with the north needle in Fix Magenta. It is 28px in the header and uses the same drawing as the favicon. The scale bar is an 8px strip with 1px ink borders and ink dashes that run 24px on and 24px clear.

### Named Rules
**The Square Plate Rule.** Anything that holds content is square-cornered and framed by a neatline. Rounding is for controls you press, and for nothing else.

## Components

### Buttons
- **Shape:** 2px radius, at least 44px tall, padding 10px by 20px, a 2px border that matches the fill, weight 700.
- **Primary:** Fix Magenta fill with Magenta Text.
- **Hover:** The fill and border mix in 12% ink to become Deep Magenta. On press the button moves down 1px.
- **Disabled:** A dashed 2px border at 40% ink, a transparent fill, soft ink text, a not-allowed cursor, and no press movement.
- **Quiet:** A transparent fill with a 2px ink border and ink text. Hover adds an 8% ink wash. It is the companion to a primary button, never a replacement for it.

### Navigation
- **Brand:** The compass star beside the wordmark, set at 800 weight, 17px, with -0.01em tracking.
- **Links:** 14.4px at weight 500, each preceded by an 8px mode marker (solid dot for static, hollow ring for on demand). The current page is weight 700 with a 2px Fix Magenta underline at a 0.35em offset. Hover shows the same underline.
- **Menu button:** Below 70rem, a button with a 1.5px ink border and a chevron that flips when the sheet is open.
- **Menu sheet:** Drops from under the 64px header with 48px rows, 1px rules between them, a 2px ink divider between the static and on-demand groups, and a 1px ink bottom edge.
- **Skip link:** An ink fill with land paper text that slides in when focused.
- **Scale bar:** Closes the header. It is the same dashed strip that opens the footer.

### Plates
A square panel on sheet vellum with a neatline, padded 20px to 32px. Two variants exist. The water plate holds the on-demand route list and takes the shallow water fill. The sea plate holds the thank-you message after a form is sent, set at 20px and weight 700.

### Fix Plate
The signature piece of the home page. The upper half is sheet vellum land holding one sentence with the build timestamp in bold tabular numerals, because it was printed ahead. A 64px coastline, drawn as a land silhouette plus four fading echoes of itself in ink, divides land from sea. Below it the sea holds a short caption, then the deeper clock band, set off by a dotted line, where the server island renders the live time in bold italic tabular numerals. While the island loads, the band shows an italic loading line. The plate has no padding of its own: each band pads itself, 24px to 32px.

### Rows
List rows for posts and products. The whole row is the link, through a stretched hit area. Each row has a 2px ink rule at the top of the list and 1px hairlines between rows, with 24px of padding. From 48rem the row splits 5fr to 7fr, title on the left and summary on the right. The product catalog uses 4fr, 5fr, and a 7rem price column that is right-aligned in tabular figures.
- **Hover:** A 4% ink wash, a 3px Fix Magenta underline under the title, and the chevron nudges 4px to the right.
- **Focus:** A 2px Fix Magenta outline around the whole row.

### Readouts
Definition lists drawn on hairline rules. The term is soft ink at weight 600 and 15px, in a column 112px to 176px wide from 40rem. The value is bold italic with tabular numerals and may break anywhere, so long tokens never overflow the plate.

### Forms
- **Fields:** Inputs and the textarea use a sheet vellum fill, a 1.5px ink border, a 2px radius, and at least 44px of height. The textarea starts 128px tall and resizes vertically only.
- **Labels:** Weight 700, set above the field with an 8px gap, with 24px between fields.
- **Error:** A 2px Hazard Red border, and a message below with a triangle glyph at weight 600.
- **Answer area:** Four states. Empty reads "No answer yet." in soft ink. Loading is soft ink at weight 500. Error uses Hazard Red with its glyph. Success is bold italic.

### Course
The request path is an ordered list of numbered ring bullets: 28px circles with a 1px ink ring on sheet vellum, 14px bold tabular numerals, joined by a 2px ink line. The fork, where static and on-demand paths part, is a nested list with a 1px rule on its left edge and mode markers on each branch.

### Mode Marker and Badge
A 10px circle with a 2px ink ring. It is solid for static and hollow for on demand, and it always sits next to words in the matching posture: upright for static, italic for on demand. The footer badge repeats the mode in words, so the ground color is never the only carrier.

### Links
Fix Magenta, underlined at 1.5px with a 0.2em offset, thickening to 2.5px on hover. The back link carries a chevron drawn from borders.

### Compass Rose
A large, inked compass rose that appears on the 404 and 500 pages only. It sits at 20% ink on wide screens and 26% ink on phones.

The Rose Reservation Rule: on wide screens the text column wraps before the rose and never runs over it. On phones the rose sits in the flow, top-right and directly above the headline, so it reads as part of the heading block. The small compass star remains the only mark in the header and favicon.

### States and Motion
- **Focus:** A 2px Fix Magenta outline offset 3px on every interactive element.
- **Selection:** A Fix Magenta wash at 28% strength.
- **Motion:** Two authored moves, both with an exponential ease-out and both inside a no-preference reduced-motion query. The first is a 320ms cross-fade between pages, so the ground floods from paper to water and back as the mode changes. The second is the 220ms chevron nudge on a hovered row. Both animate only opacity or transform.
- **Forced colors:** Markers and the scale bar draw in the system text color, the graticule is hidden, and button borders thicken to 4px (2px for the quiet button).

## Do's and Don'ts

### Do:
- **Do** tie the ground to the mode: paper, a solid dot, and upright type for static; water, a hollow ring, and italic live values for on demand.
- **Do** use Fix Magenta only for what can be acted on (buttons, links, focus) and for the compass needle.
- **Do** draw panels as neatline plates, and make depth from tint steps and ink rules.
- **Do** set every server-produced value in bold italic with tabular numerals.
- **Do** keep every interactive target at least 44px tall and every focus ring visible.
- **Do** test any new text and ground pairing for 4.5:1, especially soft ink on open sea and on the deep clock band.
- **Do** keep motion to the two authored moves, and give any new drawn element a forced-colors path.

### Don't:
- **Don't** add shadows, glows, blur, glass, or decorative gradients.
- **Don't** add a second accent or fill a panel with magenta.
- **Don't** round plates or rows, and don't turn anything into a pill.
- **Don't** add nautical props such as ropes, anchors, or parchment, and don't add serif, script, or chart lettering.
- **Don't** put the large compass rose anywhere except the 404 and 500 pages.
- **Don't** use uppercase, letterspaced eyebrow labels.
- **Don't** let color alone carry a status or a mode. Pair it with a marker, a glyph, or words.
