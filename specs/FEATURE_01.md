# Feature 1: Round-Trip CSV Input

## Feature

The program accepts a CSV file as input and outputs its contents unchanged.

## Command

```sh
slice data.csv
```

## Behavior

- The first row is treated as the header row.
- All subsequent rows are treated as data rows.
- If no transformation command is provided, the program outputs the same tabular data it received.
- Column order is preserved.
- Row order is preserved.
- Cell values are preserved exactly as data values.
- The default output format is CSV.

## Acceptance Criteria

Given an input CSV file, running the program with only the file path outputs CSV containing the same headers and rows in the same order.
