# ProtonTune.DebugServer

The application's UI, served over HTTP instead of drawn in a window. Nothing ships from here — it
exists so the interface can be opened in a browser and driven by tooling, which a Photino window
cannot be.

```sh
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5199 \
    dotnet run --project src/ProtonTune.DebugServer --no-launch-profile
```

Then http://127.0.0.1:5199. It reads the same Steam installation, the same setting definition
files and the same stored options as the desktop app, because it builds them with the same
`AddProtonTuneServices()` and `AddProtonTuneUI()` and renders the same `ProtonTune.UI.App`. What
appears is the real interface against real data, not a fixture.

## What differs from the window

Two things, and both are the host rather than the interface.

Photino renders in WebKitGTK and this renders in whatever browser is pointed at it, so a
rendering difference between the two engines will not show up here. Everything about layout,
styling, state and behaviour does.

Interactive Server is what the window's own hosting is closest to: components run in this
process with the real services, reading the real Steam installation, rather than in a browser
sandbox that would need every one of them proxied over HTTP first. Prerendering is off, because
the window does not prerender either — left on, a component would initialise twice here and once
there, which is the sort of difference a second host exists to avoid rather than introduce.

The window answers the private `artwork://` scheme itself through a registered handler, which no
browser knows about. `HttpArtworkService` rewrites those addresses to `/scheme/artwork/…` and
`CustomSchemeEndpoints` answers them from the very same `ICustomSchemeHandler` implementations, so
cover art loads here too.

## Driving it

Chrome DevTools drives the page, and needs Node on the path.

The `chrome-devtools` MCP server is the way in: navigate a page to the address above, take a
snapshot for element identifiers, then click, fill and screenshot against those. It launches a
browser of its own and returns screenshots directly, so nothing has to be saved and reopened.

The same package ships a CLI, which needs no MCP connection and is the fallback when that server
cannot start:

```sh
npx -p chrome-devtools-mcp chrome-devtools start --headless=false
npx -p chrome-devtools-mcp chrome-devtools navigate_page 1 --url http://127.0.0.1:5199/
npx -p chrome-devtools-mcp chrome-devtools take_snapshot 1
npx -p chrome-devtools-mcp chrome-devtools click 1 <uid>
npx -p chrome-devtools-mcp chrome-devtools take_screenshot 1 --filePath shot.png
```

The CLI daemon is headless unless told otherwise, which `--headless=false` is for. It keeps a
browser separate from the MCP server's, so running both leaves two.

One trap either way: filling a field with an empty string types nothing, so the box empties
without the page hearing about it and the component keeps the old value. Type a character and
delete it to clear a field.

## Writing

It edits the real Steam configuration, exactly as the desktop app does. Saving here saves for
real, and where live editing is unavailable it will close and reopen Steam to do it.
