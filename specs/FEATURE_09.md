# Feature 9: Output Formats

## Feature

The program can render results in multiple output formats.

## Commands

```sh
slice data.csv --format csv
slice data.csv --format json
slice data.csv --format table
```

## Behavior

- The `--format` option controls how the final result is displayed.
- Supported formats are:
  - `csv`
  - `json`
  - `table`
- If no format is specified, `csv` is used.
- The selected format does not change which data is computed.
- `csv` output includes headers when the result is tabular.
- `json` output represents tabular results as an array of objects.
- `table` output renders tabular results in a human-readable table.
- Single-value aggregate results are rendered as a single value in all formats unless the format requires structure.

## Acceptance Criteria

Given the same input and transformation command, changing only `--format` changes the rendering but not the underlying result values.
