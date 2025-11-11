# Changelog
All notable changes to this package will be documented in this file.

The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [0.2.3] - 2025-11-11
### Added
- Settings window: auto-start toggle, port configuration, log level, Open Docs, Start/Stop.
- Actions: `ping`, `addComponent`, `removeComponent` (with `all`), `setMultipleProperties` (array or structured forms).
- Samples: `ExtensionsTemplate` (asmdef + template action).
- Documentation: compact Action Reference table; safety & operability notes; `.http` examples.
- README: badges, What’s included table, Quickstart pinned to v0.2.3, Why MDMCP, clients examples.
- CI workflow scaffolding (validate package structure) – to be wired by repo CI.

### Changed
- Server now reads port from `EditorPrefs` and auto-starts based on settings.
- Action registration prefers project implementations over package on name conflicts.

### Fixed
- Minor robustness improvements and consistent response envelope wrapping.

[0.2.3]: https://github.com/clokk/mdmcp-unity/releases/tag/v0.2.3

# Changelog

All notable changes to this project will be documented in this file.

## [0.1.0] - Initial release
- Introduced MDMCP Server for Unity Editor
- Menu path: Markdown > Start/Stop MCP Server
- Core API: ActionResponse, IEditorAction, MCPLog, MCPUtils, EditorActionPayload
- Extensibility: auto-discovery of IEditorAction implementations
- Sample: HelloAction


