# Project Instructions

## Overview

Meridian Capital Terminal — desktop stock market dashboard built on Uno Platform with MVUX, SkiaSharp, and Material theme. Editorial luxury aesthetic (warm cream, Instrument Serif, forest green + gold accents). Single-page dashboard with portfolio tracking, per-stock chart drill-down, interactive watchlist, and mock trade execution drawer. All data is mock for v1. Desktop-first (Windows/macOS/Linux via Skia), WebAssembly as stretch goal.

## Architecture

- Pattern: MVUX (Model-View-Update eXtended)
- Navigation: Single page — no multi-page routing. All state changes within DashboardPage.
- DI: Microsoft.Extensions.DependencyInjection via Uno.Extensions.Hosting
- Charting: Liveline (SKCanvasElement, project ref `../Liveline/Liveline.csproj`) for main chart. SkiaSharp custom for sparklines/sector ring/volume.
- Trade Drawer: DrawerFlyoutPresenter (Uno Toolkit)
- Timers: Consolidated single 32ms tick (~30fps) for scroll/braille animations, separate 1s clock
- Ticker Tape: Dual-TextBlock marquee (A/B leapfrog) with Grid.Clip

## Project Structure

- `Meridian/` — Single-project Uno Platform app
- `Meridian/Models/` — Immutable record types (Stock, Holding, Sector, etc.)
- `Meridian/Presentation/` — MVUX models (DashboardModel, TradeDrawerModel)
- `Meridian/Services/` — IMarketDataService + MockMarketDataService
- `Meridian/Controls/` — Custom UserControls and SKXamlCanvas controls
- `Meridian/Views/` — DashboardPage, TradeDrawer
- `Meridian/Themes/` — ColorPaletteOverride, FontResources, TextBlockStyles, CardStyles
- `Meridian/Assets/Fonts/` — Instrument Serif, IBM Plex Mono, Outfit .ttf files
- `Meridian/Strings/en/` — Localization resources

## Uno Platform Gotchas (learned the hard way)

- **FontIcon on top of TextBox blocks input** even with `IsHitTestVisible="False"`. Render FontIcon FIRST, TextBox LAST. Use transparent TextBox bg + separate Border for visuals.
- **RenderTransform Y translate on hover causes jitter.** Don't use translate for hover effects on small elements. Use border/background color changes instead.
- **FontWeight="Thin" breaks Outfit font.** Use `SemiLight` (350) as minimum safe weight.
- **ShadowContainer is a XAML wrapper only.** Can't add/remove shadows from C# code-behind. Must wrap in `<utu:ShadowContainer>` in XAML.
- **PointerExited fires on selected elements.** Guard with `if (element == _selectedElement) return;` in PointerExited handlers.
- **ScrollViewer doesn't clip RenderTransform content.** Use `Grid.Clip` with `RectangleGeometry` (update rect on `SizeChanged`).
- **TextBlock Inlines (Runs) cause flicker on scroll reset.** For scrolling text, use plain `.Text`, never Inlines.
- **WinUI max element render width is ~16k pixels.** Don't create TextBlocks wider than ~10,000px. Use dual-TextBlock marquee pattern.
- **Mask reveal animations don't work.** Overlay Border with TranslateTransform doesn't composite correctly. Use chart library's built-in animation instead.
- **LiveCharts2 Y axis: `MinStep = range/N` gives N+2 labels.** Set `MinLimit`/`MaxLimit` and use `range/(desiredLabels-1)`.
- **StackPanel doesn't participate in Grid alignment.** Convert to Grid with Auto + * rows when bottom-alignment is needed.

## Conventions

- New pages get a corresponding partial record model in `Presentation/`.
- Use MVUX feeds/states — never raw INotifyPropertyChanged.
- Prefer Uno Toolkit controls (`NavigationBar`, `TabBar`, `AutoLayout`, `DrawerFlyoutPresenter`) over raw WinUI equivalents.
- Keep XAML lean — use Lightweight Styling and theme resources over inline values.
- Never hardcode hex colors — use `{StaticResource MeridianXxxBrush}` resources.
- Use `{StaticResource SectionLabel}` style for all card headers.
- Search Uno Platform docs via MCP before assuming API usage or patterns.

## Key References

Before starting any new feature or architectural decision, read these first:

- `docs/ARCHITECTURE.md` — system architecture, layers, dependencies, implementation priority
- `docs/DESIGN-BRIEF.md` — design language, spacing, color tokens, component patterns, ASCII layouts
- `docs/INTERACTION-SPEC.md` — state model, user flows, component states, animation inventory
- `docs/XAML-SCAFFOLD.md` — complete DashboardPage component tree, theme XAML, model code

## Verification

```bash
dotnet build
dotnet run --project Meridian/Meridian.csproj -f net10.0-desktop
```

Always run `dotnet build` after changes to confirm the project still compiles. Run tests when they exist.
