# ADR 0002: Request/response and event-driven communication

- Status: Accepted
- Date: 2026-09-04

## Decision

Use HTTP request/response for queries and commands that need an immediate answer. Use RabbitMQ integration events for cross-module workflows. Persist outgoing events using a transactional outbox and deduplicate incoming events using an inbox/idempotency key.

SignalR provides realtime delivery only; persisted module data remains the source of truth.
