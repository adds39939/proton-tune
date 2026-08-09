# ProtonTune.UI

Razor class library holding every component. It is rendered by `ProtonTune.App`, a Photino
desktop shell — not a web app. Routing is client-side only: there is no server and no browser
chrome, so a bad route shows the `NotFound` page rather than an HTTP error.

```
App.razor(.css)               Router. Root component — no page markup belongs here.
Layout/MainLayout.razor       Shell: titlebar, nav tabs, and the @Body pages render into.
Pages/*.razor                 Routable pages. Exactly one @page directive each.
Components/<Area>/*.razor     Reusable components, grouped by area. Never routable.
Formatting/                   Display helpers used by more than one component.
DependencyExtensions/         AddProtonTuneUI() — UI-only service registration.
wwwroot/app.css               Global styles ONLY (see below).
wwwroot/index.html            Document shell. Stylesheet links live here.
```

## Routing

A page is a `.razor` file in `Pages/` with a `@page` directive. Anything without one goes in
`Components/` — being reusable and being routable are different jobs, and mixing them makes a
component impossible to drop onto a second page later.

**Adding a page is two edits:** the `@page` directive, and an entry in the `NavItems` list in
`Layout/MainLayout.razor.cs`, which is what draws the tab bar. A route that exists without a nav
entry is reachable but invisible.

The root route `/` needs `NavLinkMatch.All`; with the default `Prefix` its tab stays highlighted
on every other page.

Keep pages thin. `Pages/Home.razor` is a `@page` directive and `<GameLibrary/>` — the work lives
in the component, which keeps it testable and reusable.

`Router.NotFound` is obsolete in .NET 10. Use `NotFoundPage="@typeof(NotFound)"`, which takes a
page type rather than inline content.

## Components

Components are grouped by what they are responsible for, not by what they are:

```
Components/Game/           One game's presentation primitives — GameArtwork, GameTags.
Components/Library/        Browsing the collection — GameLibrary, its cards, LibraryViewMode.
Components/Configuration/  Changing things — GameConfigDialog, SettingsPanel.
```

Put a component in the area whose job it serves. `GameArtwork` sits in `Game/` rather than
`Library/` because the config dialog uses it too — anything shared by two areas belongs to the
concept it describes, not to whichever area happened to need it first.

**Put logic in a `<Component>.razor.cs` code-behind, not an `@code` block.** The `.razor` file
stays markup. The code-behind is `public partial class <Component> : ComponentBase` in a
namespace matching the folder path, e.g. `ProtonTune.UI.Components.Library`. Moving a component
between areas means updating that namespace and `_Imports.razor` together, or the markup stops
resolving it.

Consequences worth remembering:

- Inject with an `[Inject]` property in the code-behind, not `@inject` in markup.
- `_Imports.razor` only covers `.razor` files. Code-behind files need their own `using`
  directives — `Microsoft.AspNetCore.Components` at minimum.
- Helpers used by a single component are `private static` on its code-behind. Once a second
  component needs one, move it to `Formatting/` (`PathDisplay`, `FileSizeDisplay`,
  `LastPlayedDisplay`) rather than duplicating it.

Small markup expectations: `@key` on items rendered in a loop, `title` on text that can be
truncated by `text-overflow`, and `aria-hidden` on purely decorative elements.

**Clickable cards use an overlay button, not a wrapper.** A `<button>` may only contain phrasing
content, so it cannot wrap a card holding an `<h2>` or a `<dl>`. The cards render a transparent
button stretched over themselves (`position: absolute; inset: 0`) carrying an `.sr-only` label.
That keeps the markup valid, the whole card clickable, and every card reachable by Tab.

## Styling

**Use component-scoped CSS.** Every component gets a sibling `<Component>.razor.css`. Styles
belong to the component that owns the markup.

**`wwwroot/app.css` is for global styles only** — design tokens, the reset, `body` defaults, and
document-wide utilities such as `.sr-only`. Adding a component's rules there is a bug, even if it
works.

**Never hardcode a colour.** `app.css` defines the palette as custom properties on `:root`
(`--surface-0..2`, `--border`, `--text`, `--text-muted`, `--accent`, `--accent-soft`,
`--warning`, `--warning-soft`, `--danger`). Scoped stylesheets consume them via `var(--token)`.
A new colour is added to the token list first, then referenced — that keeps the theme in one
place and dark-mode consistent.

Scoping gotcha: the scope attribute is applied to elements in *that component's own* markup, so
a parent's stylesheet cannot reach into a child component. Use `::deep` on an element the parent
does own when that is genuinely needed, and prefer moving the rule into the child instead.
`MainLayout.razor.css` shows the legitimate case — `NavLink` renders its own anchor, which never
carries the layout's scope attribute, so the tab styles hang off `.nav ::deep`.

A component that renders its own `<li>` keeps its item styling in its own stylesheet, leaving the
container to do nothing but lay children out. That is why `GameGridCard` and `GameListCard` own
the list item rather than `GameLibrary` wrapping them in one.

Photino serves these through the static web assets runtime manifest, so scoped CSS is *not*
copied into the output `wwwroot` — only `app.css` and `index.html` appear there, which is
expected. If styles ever vanish wholesale, check that `ProtonTune.App` still uses
`Microsoft.NET.Sdk.Razor`; that SDK generates both the style bundle and the manifest that serves
it.

## Data

Components never touch the filesystem or parse Steam files. They depend on an interface from
`ProtonTune.Services` (e.g. `ISteamLibraryService`, `IGameArtworkService`) and render what it
returns.

Every load path handles four states explicitly: loading, error, empty, and populated. Steam may
be missing, mid-write, or installed with no games — a component that only renders the happy path
is incomplete. Catch at the component boundary and surface a message rather than letting an
exception reach the Photino window.

Cover art is the one network dependency, fetched from Steam's CDN by the `<img>` itself. Treat it
as optional in every sense: Steam publishes no artwork for several compatibility tools, and the
user may be offline. `GameArtwork` falls back to a lettered tile on load failure, so a missing
image is a normal outcome rather than an error. Nothing else here should assume internet access.
