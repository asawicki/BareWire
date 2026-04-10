# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.7] - 2026-04-10

### Added

- `IQueueConfigurator` fluent API for queue arguments (e.g., TTL, max-length, dead-letter exchange) — task 3.14

### Fixed

- Race condition in E2E019/E2E020 tests and flaky timeout in E2E004

### Changed

- Mobile-responsive styles for hero, features, concepts, and stats sections on docs landing page

## [1.2.6] - 2026-04-09

### Added

- `MapExchange<T>` for type-to-exchange routing — task 3.13

## [1.2.5] - 2026-04-08

### Changed

- Package project URLs point to docs site (barewire.wizardsoftware.pl)
- GitHub Packages push owner fixed to Wizard-Software

### Added

- Hero landing page, Wizard-Software branding, and sidebar nav for DocFX site
- DocFX site bootstrap with GitHub Pages deployment

## [1.2.4] - 2026-04-07

### Added

- Publish-side serializer override for MassTransit publish-only bridge

### Fixed

- Hash non-Guid CorrelationId in PartitionerMiddleware for consistent partitioning

### Changed

- Automatic GitHub Release with release notes on tag push (CI)
- MIT license added

## [1.2.3] - 2026-04-06

### Added

- MassTransit envelope serializer with per-endpoint activation
- User documentation for inbox, custom serializers, and MassTransit interop
- MassTransit interop package with envelope deserialization and E2E tests
- NuGet package metadata, per-package README, and icon

### Fixed

- Stabilized E2E-008 RetryAndDlq flaky test (task 10.21)
- Release workflow runs only unit and contract tests

### Changed

- Enhanced unit tests for service collection extensions and pipeline components

[1.2.7]: https://github.com/Wizard-Software/BareWire/compare/v1.2.6...v1.2.7
[1.2.6]: https://github.com/Wizard-Software/BareWire/compare/v1.2.5...v1.2.6
[1.2.5]: https://github.com/Wizard-Software/BareWire/compare/v1.2.4...v1.2.5
[1.2.4]: https://github.com/Wizard-Software/BareWire/compare/v1.2.3...v1.2.4
[1.2.3]: https://github.com/Wizard-Software/BareWire/releases/tag/v1.2.3
