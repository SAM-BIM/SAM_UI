# Engine comparison test documents (fabricated)

B1a documents copied verbatim from SAM_Validation `SAM.Analytical.Benchmark.Compare.Tests/Fixtures/Regression/`
(branch `claude/tas-openstudio-comparison`, SAM#211). They are **fabricated test documents, not Tas or
EnergyPlus results**: a feature-rich pair spanning Match/Warn/Fail bands, a year-wrapping peak hour, an
unavailable model metric and spaces present on only one side. `weather-mismatch-*` is the same OpenStudio
document with a different weather hash, so SAM_Validation's provenance gate reports a mismatch.

`EngineComparisonTests` checks that the comparison view shows exactly what SAM_Validation computes for them.
