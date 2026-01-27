---
allowed-tools: Read, Write, Edit
description: Update @docs/Performance.md with the latest data from D:\Projects\ActualLab.Fusion.Samples\Benchmarks.md
---

Update benchmark data in all documentation files from @D:\Projects\ActualLab.Fusion.Samples\Benchmarks.md

Files to update:
- docs/Performance.md - Main performance documentation
- docs/Benchmarks.md - Detailed benchmark results (update version, date, all tables)
- docs/index.md - Landing page performance highlights
- docs/PartF-SS.md - Server-side performance section
- docs/ActualLab.Fusion-vs/SignalR.md - SignalR comparison benchmarks
- docs/ActualLab.Fusion-vs/Redis.md - Redis comparison benchmarks
- docs/ActualLab.Fusion-vs/gRPC.md - gRPC comparison benchmarks
- README.md - Benchmark highlights and ActualLab.Rpc comparison

Instructions:
- Update the numerical values in all existing benchmark tables to match the source file
- Update version number and date in docs/Benchmarks.md header
- If benchmark descriptions are updated in the source, copy the changes
- Do not copy instructions on how to run benchmarks
- If the source file contains new sections not present in target files, ignore them
- Preserve the existing structure and explanatory text in each file
- Recalculate speedup ratios where values have changed
