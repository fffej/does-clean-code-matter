# Feature 10: Command Pipeline

## Feature

The program can compose multiple commands into a pipeline.

## Command

```sh
slice data.csv where age>30 | sort age | head 3
```

## Behavior

- Multiple commands may be applied in sequence.
- Each command receives the result of the previous command.
- Commands are evaluated from left to right.
- Filtering, sorting, limiting, distinct selection, aggregation, grouping, and column selection may be composed where meaningful.
- Once a command produces a single aggregate value, no later row-based command may be applied.
- If a command cannot operate on the current result shape, the command fails with an error.
- The final result is rendered according to the selected output format.

## Acceptance Criteria

Given rows with an `age` column, running `where age>30 | sort age | head 3` outputs the first three rows after filtering to ages greater than `30` and sorting the remaining rows by age ascending.
