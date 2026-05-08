# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-05-08

### Added
- Azure IP Group discovery via `DefaultAzureCredential` with auto-subscription enumeration
- Per-group detail page with inline label and notes editing via Bootstrap modal
- Cross-group Unlabeled view surfacing all IPs without a label
- SQLite persistence via Dapper with automatic schema initialization on startup
- Sync-on-view: new IPs from Azure are inserted as unlabeled rows when a group's detail page loads
- Entra ID authentication via Microsoft.Identity.Web
- Search across IP Groups on the index page
- CoreUI-based responsive UI
- Data protection key persistence for App Service deployments
- Configurable subscription exclusion list (`Azure:ExcludeSubscriptionIds`)

[Unreleased]: https://github.com/chandlermania/igma/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/chandlermania/igma/releases/tag/v1.0.0
