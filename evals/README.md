# InfoOrganizer eval corpus
This corpus measures the claim that messy spreadsheets can be adapted automatically.
All fixture data is generated locally by the eval tool; no external data is used.
Fixtures are small es-MX flavored ledgers with deliberate formatting problems.
Each fixture has a source.xlsx or source.csv and a hand-authored expected.json answer key.
The runner uses the real import pipeline with the unconfirmed heuristic proposal.
The fake AI client is unconfigured, so evals never make network or AI calls.
Run the corpus with: dotnet run --project tools/InfoOrganizer.Evals -- run
Write tracking JSON with: dotnet run --project tools/InfoOrganizer.Evals -- run --json artifacts/evals/latest.json
Regenerate fixtures with: dotnet run --project tools/InfoOrganizer.Evals -- generate
Regeneration rewrites evals/fixtures deterministically from FixtureGenerator.cs.
Known-issue fixtures stay in reports but are excluded from the hard CI gate.
The xlsx files are produced with ClosedXML and csv files use deterministic UTF-8 text.
The CI gate is tests/InfoOrganizer.Tests/EvalCorpusTests.cs via the normal dotnet test path.
When adding fixtures, update expected.json from construction facts, not runner output.