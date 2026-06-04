# Feature 7: Aggregation

## Feature

The program can compute aggregate values across all rows.

## Commands

```sh
slice data.csv count
slice data.csv sum amount
```

## Behavior

- The `count` command outputs the number of data rows.
- The `sum` command takes a column name and outputs the total of that column.
- `sum` requires all included values in the target column to be numeric.
- If the target column does not exist, the command fails with an error.
- Aggregate commands output a single result value.
- If previous commands filter or limit the data, the aggregate is computed over the remaining rows.

## Acceptance Criteria

Given a CSV file with ten data rows, running `count` outputs `10`.

Given a CSV file with an `amount` column, running `sum amount` outputs the total of all `amount` values.
