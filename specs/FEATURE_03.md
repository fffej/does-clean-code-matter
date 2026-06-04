# Feature 3: Row Filtering

## Feature

The program can filter rows using a comparison expression.

## Command

```sh
slice data.csv where age>30
```

## Behavior

- The `where` command keeps only rows that satisfy the expression.
- Supported comparison operators are:
  - `=`
  - `!=`
  - `>`
  - `<`
  - `>=`
  - `<=`
- The left-hand side of the expression is a column name.
- The right-hand side of the expression is a literal value.
- Numeric comparisons are used when both compared values are numeric.
- Otherwise, values are compared as text.
- The header row is always preserved in CSV output.
- Row order is preserved among matching rows.

## Acceptance Criteria

Given rows with an `age` column, running `where age>30` outputs only rows where `age` is greater than `30`.
