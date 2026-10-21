# ADR 001: Feature ownership

Accepted. Keep request-specific orchestration and persistence in feature handlers. Avoid horizontal layers until independent policy requires them.

Course capacity is an invariant under concurrent requests, so PostgreSQL enforces a serialised critical section using a row lock. Database write failures roll back the transaction. SQLite tests cannot establish PostgreSQL locking behaviour; CI separately tests it.

Trade-off: the row lock limits hot-course write throughput. This reference optimises for understandable correctness, not maximum admission throughput.
