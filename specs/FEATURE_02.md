# Feature 2: Column Selection

## Feature

The program can output only selected columns from the input data.

## Command

```sh
slice data.csv select name,age
```

## Behavior

- The `select` command takes a comma-separated list of column names.
- Only the named columns are included in the output.
- Columns appear in the order specified by the command.
- Rows remain in their original order.
- If a selected column does not exist, the command fails with an error.

## Acceptance Criteria

Given a CSV file with columns `name`, `age`, and `city`, running `select name,age` outputs only the `name` and `age` columns, in that order.
