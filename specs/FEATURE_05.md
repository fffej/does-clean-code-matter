# Feature 5: Limit Rows

## Feature

The program can output only the first `N` rows.

## Command

```sh
slice data.csv head 5
```

## Behavior

- The `head` command takes a positive integer.
- It keeps the first `N` data rows.
- Row order is preserved.
- The header row is preserved in CSV output.
- If fewer than `N` rows exist, all rows are output.
- If `N` is zero or negative, the command fails with an error.

## Acceptance Criteria

Given a CSV file with more than five data rows, running `head 5` outputs the header and the first five data rows.
