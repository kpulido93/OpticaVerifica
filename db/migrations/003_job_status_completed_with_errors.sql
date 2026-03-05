-- Add explicit status for jobs that finish with partial item failures
ALTER TABLE jobs
MODIFY COLUMN status ENUM(
    'PENDING',
    'PROCESSING',
    'COMPLETED',
    'COMPLETED_WITH_ERRORS',
    'FAILED',
    'CANCELLED',
    'PAUSED_BY_SCHEDULE'
) NOT NULL DEFAULT 'PENDING';
