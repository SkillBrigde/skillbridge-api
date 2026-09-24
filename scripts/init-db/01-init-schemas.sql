-- REFERENCE BLUEPRINT ONLY: Docker does not execute this file.
-- Runtime tables are owned by module EF Core migrations. Do not apply both to one database.
-- ==============================================================================
-- SkillBridge Enterprise Platform - Complete Database Architecture Blueprint
-- Chuẩn Doanh Nghiệp: Đầy đủ 10 Bounded Contexts, Ledger Kế Toán Kép, State Machine,
-- Hỗ trợ đa múi giờ, Audit Log, Transactional Outbox & Inbox, RBAC & Bảo mật.
-- ==============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ==============================================================================
-- 1. MODULE IDENTITY (Schema: identity)
-- Xác thực, Phân quyền RBAC, Quản lý Token, Bảo mật tài khoản & Đăng nhập mạng xã hội
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE IF NOT EXISTS identity.roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL UNIQUE,
    normalized_name VARCHAR(50) NOT NULL UNIQUE,
    description VARCHAR(250) NULL
);

INSERT INTO identity.roles (name, normalized_name, description)
VALUES 
    ('Admin', 'ADMIN', 'Quản trị viên toàn hệ thống'),
    ('Mentor', 'MENTOR', 'Chuyên gia hướng dẫn / Cố vấn'),
    ('Mentee', 'MENTEE', 'Người học / Người được cố vấn')
ON CONFLICT (name) DO NOTHING;

CREATE TABLE IF NOT EXISTS identity.users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(256) NOT NULL,
    normalized_email VARCHAR(256) NOT NULL,
    password_hash VARCHAR(500) NOT NULL DEFAULT '',
    security_stamp VARCHAR(100) NOT NULL DEFAULT gen_random_uuid()::text,
    full_name VARCHAR(200) NOT NULL,
    phone_number VARCHAR(30) NULL,
    avatar_url VARCHAR(500) NULL,
    is_email_confirmed BOOLEAN NOT NULL DEFAULT FALSE,
    is_phone_confirmed BOOLEAN NOT NULL DEFAULT FALSE,
    two_factor_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    lockout_end_utc TIMESTAMPTZ NULL,
    lockout_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    access_failed_count INT NOT NULL DEFAULT 0,
    role VARCHAR(50) NOT NULL DEFAULT 'Mentee',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_users_email ON identity.users(email);
CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_users_normalized_email ON identity.users(normalized_email);

CREATE TABLE IF NOT EXISTS identity.user_roles (
    user_id UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES identity.roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE IF NOT EXISTS identity.refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    token VARCHAR(500) NOT NULL UNIQUE,
    expires_at_utc TIMESTAMPTZ NOT NULL,
    revoked_at_utc TIMESTAMPTZ NULL,
    replaced_by_token VARCHAR(500) NULL,
    created_by_ip VARCHAR(50) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_identity_tokens_user ON identity.refresh_tokens(user_id);

CREATE TABLE IF NOT EXISTS identity.external_logins (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL, -- 'Google', 'GitHub', 'LinkedIn'
    provider_key VARCHAR(200) NOT NULL,
    provider_display_name VARCHAR(100) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_identity_provider_key UNIQUE (provider, provider_key)
);

-- ==============================================================================
-- 2. MODULE PROFILES (Schema: profiles)
-- Hồ sơ Mentor & Mentee, Kỹ năng, Kinh nghiệm làm việc, Học vấn, Hỗ trợ đa múi giờ
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS profiles;

CREATE TABLE IF NOT EXISTS profiles.mentor_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE, -- Unconstrained ID trỏ sang identity.users(id)
    headline VARCHAR(200) NOT NULL DEFAULT '',
    bio TEXT NOT NULL DEFAULT '',
    hourly_rate NUMERIC(12, 2) NOT NULL DEFAULT 0.00,
    currency VARCHAR(3) NOT NULL DEFAULT 'VND',
    timezone VARCHAR(50) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh', -- Cực kỳ quan trọng cho lịch hẹn xuyên múi giờ
    years_of_experience INT NOT NULL DEFAULT 0,
    video_intro_url VARCHAR(500) NULL,
    github_url VARCHAR(300) NULL,
    linkedin_url VARCHAR(300) NULL,
    website_url VARCHAR(300) NULL,
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    verification_status VARCHAR(30) NOT NULL DEFAULT 'Unverified', -- 'Unverified', 'Pending', 'Verified', 'Rejected'
    rating_average NUMERIC(3, 2) NOT NULL DEFAULT 0.00,
    total_reviews INT NOT NULL DEFAULT 0,
    total_mentees INT NOT NULL DEFAULT 0,
    total_sessions INT NOT NULL DEFAULT 0,
    is_accepting_mentees BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS ix_profiles_mentor_rate ON profiles.mentor_profiles(hourly_rate);
CREATE INDEX IF NOT EXISTS ix_profiles_mentor_rating ON profiles.mentor_profiles(rating_average DESC);

CREATE TABLE IF NOT EXISTS profiles.mentee_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE,
    headline VARCHAR(200) NOT NULL DEFAULT '',
    career_goal TEXT NOT NULL DEFAULT '',
    current_level VARCHAR(50) NOT NULL DEFAULT 'Beginner',
    timezone VARCHAR(50) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    budget_min NUMERIC(12, 2) NULL,
    budget_max NUMERIC(12, 2) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS profiles.mentor_skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentor_profile_id UUID NOT NULL REFERENCES profiles.mentor_profiles(id) ON DELETE CASCADE,
    skill_id UUID NOT NULL, -- Tham chiếu sang catalog.skills(id)
    skill_name VARCHAR(100) NOT NULL,
    proficiency_level VARCHAR(30) NOT NULL DEFAULT 'Intermediate', -- 'Beginner', 'Intermediate', 'Advanced', 'Expert'
    years_of_experience INT NOT NULL DEFAULT 1,
    is_primary BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX IF NOT EXISTS ix_profiles_mentor_skills_skill ON profiles.mentor_skills(skill_id);

CREATE TABLE IF NOT EXISTS profiles.mentor_experiences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentor_profile_id UUID NOT NULL REFERENCES profiles.mentor_profiles(id) ON DELETE CASCADE,
    company_name VARCHAR(150) NOT NULL,
    title VARCHAR(100) NOT NULL,
    location VARCHAR(100) NULL,
    start_date DATE NOT NULL,
    end_date DATE NULL,
    is_current BOOLEAN NOT NULL DEFAULT FALSE,
    description TEXT NULL
);

CREATE TABLE IF NOT EXISTS profiles.mentor_certifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentor_profile_id UUID NOT NULL REFERENCES profiles.mentor_profiles(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    issuing_organization VARCHAR(150) NOT NULL,
    issue_date DATE NOT NULL,
    expiration_date DATE NULL,
    credential_id VARCHAR(100) NULL,
    credential_url VARCHAR(500) NULL
);

-- ==============================================================================
-- 3. MODULE CATALOG (Schema: catalog)
-- Phân cấp danh mục đa tầng (Hierarchical Categories), Kỹ năng, Thẻ phân loại
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS catalog;

CREATE TABLE IF NOT EXISTS catalog.categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_id UUID NULL REFERENCES catalog.categories(id) ON DELETE RESTRICT,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(120) NOT NULL UNIQUE,
    description TEXT NULL,
    icon_url VARCHAR(300) NULL,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);
CREATE INDEX IF NOT EXISTS ix_catalog_categories_parent ON catalog.categories(parent_id);

CREATE TABLE IF NOT EXISTS catalog.skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id UUID NOT NULL REFERENCES catalog.categories(id) ON DELETE RESTRICT,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(120) NOT NULL UNIQUE,
    description TEXT NULL,
    icon_url VARCHAR(300) NULL,
    is_popular BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_catalog_skills_category ON catalog.skills(category_id);

CREATE TABLE IF NOT EXISTS catalog.tags (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL UNIQUE,
    slug VARCHAR(60) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS catalog.skill_tags (
    skill_id UUID NOT NULL REFERENCES catalog.skills(id) ON DELETE CASCADE,
    tag_id UUID NOT NULL REFERENCES catalog.tags(id) ON DELETE CASCADE,
    PRIMARY KEY (skill_id, tag_id)
);

-- ==============================================================================
-- 4. MODULE SCHEDULING (Schema: scheduling)
-- Quy tắc rảnh rỗi, Cài đặt Buffer Time, Tạo Slot linh hoạt và Báo nghỉ
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS scheduling;

CREATE TABLE IF NOT EXISTS scheduling.mentor_schedule_settings (
    mentor_id UUID PRIMARY KEY, -- ID Mentor
    timezone VARCHAR(50) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    buffer_time_minutes INT NOT NULL DEFAULT 15, -- Thời gian nghỉ giữa 2 buổi hẹn
    lead_time_hours INT NOT NULL DEFAULT 12, -- Phải đặt trước ít nhất bao nhiêu tiếng
    max_days_in_advance INT NOT NULL DEFAULT 30, -- Cho phép đặt trước tối đa bao nhiêu ngày
    default_session_duration_minutes INT NOT NULL DEFAULT 60
);

CREATE TABLE IF NOT EXISTS scheduling.availability_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentor_id UUID NOT NULL,
    day_of_week SMALLINT NOT NULL CHECK (day_of_week >= 0 AND day_of_week <= 6),
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT chk_scheduling_time CHECK (start_time < end_time)
);
CREATE INDEX IF NOT EXISTS ix_scheduling_rules_mentor ON scheduling.availability_rules(mentor_id);

CREATE TABLE IF NOT EXISTS scheduling.schedule_slots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentor_id UUID NOT NULL,
    start_time_utc TIMESTAMPTZ NOT NULL,
    end_time_utc TIMESTAMPTZ NOT NULL,
    slot_type VARCHAR(30) NOT NULL DEFAULT '1on1', -- '1on1', 'Group', 'CodeReview', 'CareerGuidance'
    status VARCHAR(30) NOT NULL DEFAULT 'Available', -- 'Available', 'Locked', 'Booked'
    booking_id UUID NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_scheduling_slot_time CHECK (start_time_utc < end_time_utc)
);
CREATE INDEX IF NOT EXISTS ix_scheduling_slots_mentor_status ON scheduling.schedule_slots(mentor_id, status, start_time_utc);

CREATE TABLE IF NOT EXISTS scheduling.time_offs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentor_id UUID NOT NULL,
    start_time_utc TIMESTAMPTZ NOT NULL,
    end_time_utc TIMESTAMPTZ NOT NULL,
    reason VARCHAR(250) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_scheduling_timeoff_time CHECK (start_time_utc < end_time_utc)
);

-- ==============================================================================
-- 5. MODULE BOOKING (Schema: booking)
-- State Machine Đơn đặt lịch, Yêu cầu dời lịch (Reschedule), Transactional Outbox
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS booking;

CREATE TABLE IF NOT EXISTS booking.bookings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_code VARCHAR(20) NOT NULL UNIQUE, -- Ví dụ: SB-2026-98124
    mentee_id UUID NOT NULL,
    mentor_id UUID NOT NULL,
    schedule_slot_id UUID NOT NULL,
    start_time_utc TIMESTAMPTZ NOT NULL,
    end_time_utc TIMESTAMPTZ NOT NULL,
    session_duration_minutes INT NOT NULL DEFAULT 60,
    total_amount NUMERIC(12, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'VND',
    status VARCHAR(30) NOT NULL DEFAULT 'Draft', 
    -- State Machine: 'Draft' -> 'PendingPayment' -> 'Confirmed' -> 'Rescheduled' -> 'InProgress' -> 'Completed' -> 'Cancelled' -> 'Refunded'
    cancellation_reason TEXT NULL,
    cancelled_by_user_id UUID NULL,
    meeting_link VARCHAR(500) NULL,
    mentee_notes TEXT NULL,
    mentor_notes TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS ix_booking_mentor ON booking.bookings(mentor_id, status);
CREATE INDEX IF NOT EXISTS ix_booking_mentee ON booking.bookings(mentee_id, status);

CREATE TABLE IF NOT EXISTS booking.booking_audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL REFERENCES booking.bookings(id) ON DELETE CASCADE,
    from_status VARCHAR(30) NOT NULL,
    to_status VARCHAR(30) NOT NULL,
    changed_by_user_id UUID NOT NULL,
    reason TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS booking.reschedule_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL REFERENCES booking.bookings(id) ON DELETE CASCADE,
    requester_user_id UUID NOT NULL,
    original_slot_id UUID NOT NULL,
    proposed_slot_id UUID NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Accepted', 'Declined', 'Expired'
    reason TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    responded_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS booking.outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type VARCHAR(200) NOT NULL,
    content JSONB NOT NULL,
    occurred_on_utc TIMESTAMPTZ NOT NULL,
    processed_on_utc TIMESTAMPTZ NULL,
    error TEXT NULL,
    retry_count INT NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_booking_outbox_unprocessed ON booking.outbox_messages(occurred_on_utc) WHERE processed_on_utc IS NULL;

CREATE TABLE IF NOT EXISTS booking.inbox_messages (
    id UUID PRIMARY KEY, -- Idempotency Key từ EventId
    type VARCHAR(200) NOT NULL,
    occurred_on_utc TIMESTAMPTZ NOT NULL,
    processed_on_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ==============================================================================
-- 6. MODULE PAYMENTS (Schema: payments)
-- Sổ cái kế toán kép (Double-Entry Ledger), Hợp đồng Escrow Tạm giữ, Webhook Idempotency
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS payments;

CREATE TABLE IF NOT EXISTS payments.wallets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE,
    balance NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    currency VARCHAR(3) NOT NULL DEFAULT 'VND',
    is_locked BOOLEAN NOT NULL DEFAULT FALSE,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_wallet_non_negative CHECK (balance >= 0.00)
);

CREATE TABLE IF NOT EXISTS payments.wallet_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    wallet_id UUID NOT NULL REFERENCES payments.wallets(id) ON DELETE RESTRICT,
    amount NUMERIC(14, 2) NOT NULL, -- Dương: Credit (Nạp/Nhận), Âm: Debit (Rút/Chi)
    balance_before NUMERIC(14, 2) NOT NULL,
    balance_after NUMERIC(14, 2) NOT NULL,
    transaction_type VARCHAR(40) NOT NULL, -- 'TopUp', 'EscrowHold', 'EscrowRelease', 'Withdrawal', 'Refund'
    reference_type VARCHAR(40) NOT NULL, -- 'Booking', 'BankTransfer', 'Dispute'
    reference_id UUID NOT NULL,
    description VARCHAR(300) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_payments_wallet_history ON payments.wallet_transactions(wallet_id, created_at_utc DESC);

-- Hợp đồng giữ tiền trung gian (Escrow) để bảo vệ quyền lợi của cả Mentor và Mentee
CREATE TABLE IF NOT EXISTS payments.escrow_contracts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL UNIQUE,
    mentee_id UUID NOT NULL,
    mentor_id UUID NOT NULL,
    total_amount NUMERIC(12, 2) NOT NULL,
    platform_fee_amount NUMERIC(12, 2) NOT NULL, -- Phí sàn SkillBridge
    mentor_net_amount NUMERIC(12, 2) NOT NULL, -- Tiền thực nhận của Mentor
    currency VARCHAR(3) NOT NULL DEFAULT 'VND',
    status VARCHAR(30) NOT NULL DEFAULT 'Held', -- 'Held', 'ReleasedToMentor', 'RefundedToMentee', 'Disputed'
    release_due_date_utc TIMESTAMPTZ NOT NULL, -- Thường sau khi buổi học xong 24h
    released_at_utc TIMESTAMPTZ NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS payments.payment_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NULL,
    wallet_id UUID NULL REFERENCES payments.wallets(id),
    payment_gateway VARCHAR(50) NOT NULL, -- 'VnPay', 'MoMo', 'Stripe', 'InternalWallet'
    gateway_transaction_id VARCHAR(200) NULL,
    amount NUMERIC(12, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'VND',
    payment_type VARCHAR(30) NOT NULL, -- 'BookingDeposit', 'WalletTopUp', 'MentorPayout'
    status VARCHAR(30) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Succeeded', 'Failed'
    failure_reason TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at_utc TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS ix_payments_gateway_ref ON payments.payment_transactions(payment_gateway, gateway_transaction_id);

-- Lưu vết Webhook/IPN để ngăn chặn Replay Attack / xử lý lặp lại
CREATE TABLE IF NOT EXISTS payments.payment_webhooks_audit (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    gateway VARCHAR(50) NOT NULL,
    payload JSONB NOT NULL,
    is_processed BOOLEAN NOT NULL DEFAULT FALSE,
    processed_at_utc TIMESTAMPTZ NULL,
    error TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS payments.outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type VARCHAR(200) NOT NULL,
    content JSONB NOT NULL,
    occurred_on_utc TIMESTAMPTZ NOT NULL,
    processed_on_utc TIMESTAMPTZ NULL,
    error TEXT NULL,
    retry_count INT NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_payments_outbox_unprocessed ON payments.outbox_messages(occurred_on_utc) WHERE processed_on_utc IS NULL;

CREATE TABLE IF NOT EXISTS payments.inbox_messages (
    id UUID PRIMARY KEY,
    type VARCHAR(200) NOT NULL,
    occurred_on_utc TIMESTAMPTZ NOT NULL,
    processed_on_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ==============================================================================
-- 7. MODULE LEARNING (Schema: learning)
-- Quản lý phòng học, Nhật ký tham dự (Attendance), Ghi chú, Tài liệu và Action Items
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS learning;

CREATE TABLE IF NOT EXISTS learning.learning_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL UNIQUE,
    mentor_id UUID NOT NULL,
    mentee_id UUID NOT NULL,
    room_id VARCHAR(100) NOT NULL UNIQUE,
    room_url VARCHAR(500) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Scheduled', -- 'Scheduled', 'InProgress', 'Completed', 'NoShow'
    actual_started_at_utc TIMESTAMPTZ NULL,
    actual_ended_at_utc TIMESTAMPTZ NULL,
    duration_seconds INT NOT NULL DEFAULT 0,
    recording_url VARCHAR(500) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS learning.session_attendances (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES learning.learning_sessions(id) ON DELETE CASCADE,
    user_id UUID NOT NULL,
    role VARCHAR(30) NOT NULL, -- 'Mentor', 'Mentee'
    joined_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    left_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS learning.session_notes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES learning.learning_sessions(id) ON DELETE CASCADE,
    author_id UUID NOT NULL,
    content TEXT NOT NULL,
    is_shared BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS learning.session_materials (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES learning.learning_sessions(id) ON DELETE CASCADE,
    uploader_id UUID NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    file_url VARCHAR(500) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    uploaded_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS learning.session_action_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES learning.learning_sessions(id) ON DELETE CASCADE,
    assignee_id UUID NOT NULL, -- Mentee được giao bài tập/nhiệm vụ
    title VARCHAR(300) NOT NULL,
    description TEXT NULL,
    due_date_utc TIMESTAMPTZ NULL,
    is_completed BOOLEAN NOT NULL DEFAULT FALSE,
    completed_at_utc TIMESTAMPTZ NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ==============================================================================
-- 8. MODULE MESSAGING (Schema: messaging)
-- Hội thoại, Tin nhắn, File đính kèm, Trạng thái đã xem (Read Receipts)
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS messaging;

CREATE TABLE IF NOT EXISTS messaging.conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    participant_one_id UUID NOT NULL,
    participant_two_id UUID NOT NULL,
    last_message_preview VARCHAR(250) NULL,
    last_message_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_messaging_participants UNIQUE (participant_one_id, participant_two_id)
);

CREATE TABLE IF NOT EXISTS messaging.messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID NOT NULL REFERENCES messaging.conversations(id) ON DELETE CASCADE,
    sender_id UUID NOT NULL,
    message_type VARCHAR(30) NOT NULL DEFAULT 'Text', -- 'Text', 'Attachment', 'System', 'SessionInvite'
    content TEXT NOT NULL,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS ix_messaging_messages_convo ON messaging.messages(conversation_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS messaging.message_reads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID NOT NULL REFERENCES messaging.conversations(id) ON DELETE CASCADE,
    user_id UUID NOT NULL,
    last_read_message_id UUID NOT NULL,
    read_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_messaging_read_receipt UNIQUE (conversation_id, user_id)
);

CREATE TABLE IF NOT EXISTS messaging.message_attachments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL REFERENCES messaging.messages(id) ON DELETE CASCADE,
    file_name VARCHAR(255) NOT NULL,
    file_url VARCHAR(500) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    content_type VARCHAR(100) NOT NULL
);

-- ==============================================================================
-- 9. MODULE REVIEWS (Schema: reviews)
-- Đánh giá đa tiêu chí (Multi-Criteria), Phản hồi của Mentor, Bảng tổng hợp điểm
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS reviews;

CREATE TABLE IF NOT EXISTS reviews.reviews (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL UNIQUE,
    mentor_id UUID NOT NULL,
    mentee_id UUID NOT NULL,
    overall_rating SMALLINT NOT NULL CHECK (overall_rating >= 1 AND overall_rating <= 5),
    communication_rating SMALLINT NOT NULL CHECK (communication_rating >= 1 AND communication_rating <= 5),
    expertise_rating SMALLINT NOT NULL CHECK (expertise_rating >= 1 AND expertise_rating <= 5),
    helpfulness_rating SMALLINT NOT NULL CHECK (helpfulness_rating >= 1 AND helpfulness_rating <= 5),
    comment TEXT NOT NULL DEFAULT '',
    mentor_reply TEXT NULL,
    mentor_replied_at_utc TIMESTAMPTZ NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Published', -- 'Published', 'Flagged', 'Hidden'
    is_anonymous BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS ix_reviews_mentor ON reviews.reviews(mentor_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS reviews.mentor_rating_summaries (
    mentor_id UUID PRIMARY KEY,
    total_reviews INT NOT NULL DEFAULT 0,
    average_rating NUMERIC(3, 2) NOT NULL DEFAULT 0.00,
    communication_avg NUMERIC(3, 2) NOT NULL DEFAULT 0.00,
    expertise_avg NUMERIC(3, 2) NOT NULL DEFAULT 0.00,
    helpfulness_avg NUMERIC(3, 2) NOT NULL DEFAULT 0.00,
    five_star_count INT NOT NULL DEFAULT 0,
    four_star_count INT NOT NULL DEFAULT 0,
    three_star_count INT NOT NULL DEFAULT 0,
    two_star_count INT NOT NULL DEFAULT 0,
    one_star_count INT NOT NULL DEFAULT 0,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ==============================================================================
-- 10. MODULE RECOMMENDATIONS (Schema: recommendations)
-- Điểm số hiệu năng Mentor, Ma trận sở thích Mentee và Lịch sử đề xuất
-- ==============================================================================
CREATE SCHEMA IF NOT EXISTS recommendations;

CREATE TABLE IF NOT EXISTS recommendations.mentor_metrics (
    mentor_id UUID PRIMARY KEY,
    acceptance_rate NUMERIC(5, 2) NOT NULL DEFAULT 100.00, -- Tỷ lệ chấp nhận đơn đặt lịch (%)
    completion_rate NUMERIC(5, 2) NOT NULL DEFAULT 100.00, -- Tỷ lệ hoàn thành buổi học (%)
    average_response_time_minutes INT NOT NULL DEFAULT 30, -- Thời gian phản hồi tin nhắn trung bình
    repeat_mentee_count INT NOT NULL DEFAULT 0, -- Số mentee quay lại học tiếp
    composite_rank_score NUMERIC(6, 2) NOT NULL DEFAULT 0.00, -- Điểm xếp hạng tổng hợp
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS recommendations.mentee_interests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentee_id UUID NOT NULL,
    category_id UUID NOT NULL,
    skill_id UUID NOT NULL,
    weight NUMERIC(3, 2) NOT NULL DEFAULT 1.00, -- Mức độ quan tâm (1.00 - 5.00)
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_recommendations_mentee_skill UNIQUE (mentee_id, skill_id)
);

CREATE TABLE IF NOT EXISTS recommendations.recommendation_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mentee_id UUID NOT NULL,
    recommended_mentor_ids UUID[] NOT NULL,
    algorithm_strategy VARCHAR(50) NOT NULL DEFAULT 'HybridPopularityRating',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ==============================================================================
-- PHÂN QUYỀN TOÀN DIỆN CHO USER ỨNG DỤNG 'skillbridge'
-- ==============================================================================
DO $$
DECLARE
    s TEXT;
BEGIN
    FOR s IN 
        SELECT schema_name 
        FROM information_schema.schemata 
        WHERE schema_name IN ('identity', 'profiles', 'catalog', 'scheduling', 'booking', 'payments', 'learning', 'messaging', 'reviews', 'recommendations')
    LOOP
        EXECUTE format('GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA %I TO skillbridge;', s);
        EXECUTE format('GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA %I TO skillbridge;', s);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL ON TABLES TO skillbridge;', s);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL ON SEQUENCES TO skillbridge;', s);
    END LOOP;
END $$;
