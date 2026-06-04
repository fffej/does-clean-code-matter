# Feature 6: Distinct Values

## Feature

The program can output unique values for one or more columns.

## Command

```sh
slice data.csv distinct city
```

## Behavior

- The `distinct` command takes one or more column names.
- Output contains only the specified columns.
- Duplicate rows, considering only the specified columns, are removed.
- The first occurrence of each unique value or value combination is preserved.
- Output order follows the order in which unique values first appear in the input.
- If any specified column does not exist, the command fails with an error.

## Acceptance Criteria

Given rows with a `city` column, running `distinct city` outputs each city once, in first-seen order.
