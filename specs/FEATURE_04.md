# Feature 4: Sorting

## Feature

The program can sort rows by a column.

## Command

```sh
slice data.csv sort age desc
```

## Behavior

- The `sort` command orders rows by the values in a named column.
- Sort direction may be `asc` or `desc`.
- If no direction is provided, ascending order is used.
- Numeric sorting is used when all values in the sort column are numeric.
- Otherwise, values are sorted as text.
- The header row remains first in CSV output.
- All data rows are included unless another command removes them.
- If the sort column does not exist, the command fails with an error.

## Acceptance Criteria

Given rows with an `age` column, running `sort age desc` outputs all rows ordered from highest age to lowest age.
