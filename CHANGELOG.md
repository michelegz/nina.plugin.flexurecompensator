# Flexure Compensator

## 1.0.1.0 (2026-02-23)
- Fix: preserve PHD2 "Use Multiple Stars" setting when using lock shift — ensure lock-shift is only enabled when non-zero rates are applied and properly disable it during cleanup to avoid PHD2 persisting multi-star OFF. (commit b103ad6)
- Fix: read `DownSampleFactor` from global plate solving settings so plate solving uses the configured downsample value instead of defaulting to 0. (commit 3015cc3)
- Fix: defensive event handling to improve compatibility with other plugins (commit 6628674)
- Fix: minor typos and documentation cleanups (AssemblyInfo) (commit 737395f)

## 1.0.0.0
- Initial release (2025/10/28)
- New VS solution initialized using the current NINA plugin template for .NET 8
- Core logic derived from original *Flexure Correction* plugin v0.8.2.0 (MPL 2.0) which is no longer maintained
- Improved roboustness of isLockPositionValid by checking correct guider configuration and handling edge cases
- Implemented automatic versioning based on Git tags