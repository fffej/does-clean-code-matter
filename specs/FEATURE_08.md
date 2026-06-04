# Feature 8: Group By

## Feature

The program can compute aggregate values per group.

## Command

```sh
slice data.csv groupby city sum amount
```

## Behavior

- The `groupby` command groups rows by the values in a named column.
- An aggregate is computed separately for each group.
- Supported group aggregates are:
  - `count`
  - `sum <column>`
- Output contains one row per group.
- The first output column is the group column.
- The second output column is the aggregate result.
- Groups appear in the order their first row appears in the input.
- `sum` requires numeric values in the target column.
- If any referenced column does not exist, the command fails with an error.

## Acceptance Criteria

Given rows with `city` and `amount` columns, running `groupby city sum amount` outputs one row per city with the total amount for that city.
