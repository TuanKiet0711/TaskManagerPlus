-- =========================================
-- DATABASE: APP USAGE TRACKER
-- =========================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'app_usage_tracker')
BEGIN
    CREATE DATABASE app_usage_tracker;
END
GO

USE app_usage_tracker;
GO

-- =========================================
-- 1. USERS
-- =========================================
CREATE TABLE users (
    user_id INT IDENTITY(1,1) PRIMARY KEY,
    username NVARCHAR(50) NOT NULL,
    computer_name NVARCHAR(100),
    created_at DATETIME DEFAULT GETDATE()
);

-- =========================================
-- 2. CATEGORIES
-- =========================================
CREATE TABLE categories (
    category_id INT IDENTITY(1,1) PRIMARY KEY,
    category_name NVARCHAR(50) NOT NULL
);

-- =========================================
-- 3. APPLICATIONS
-- =========================================
CREATE TABLE applications (
    app_id INT IDENTITY(1,1) PRIMARY KEY,
    app_name NVARCHAR(100) NOT NULL,
    exe_name NVARCHAR(100),
    category_id INT,
    created_at DATETIME DEFAULT GETDATE(),
    CONSTRAINT fk_app_category
        FOREIGN KEY (category_id)
        REFERENCES categories(category_id)
        ON DELETE SET NULL
);

-- =========================================
-- 4. SESSIONS (CORE TABLE)
-- =========================================
CREATE TABLE sessions (
    session_id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT,
    app_id INT,
    start_time DATETIME NOT NULL,
    end_time DATETIME,
    duration_seconds INT,
    CONSTRAINT fk_session_user
        FOREIGN KEY (user_id)
        REFERENCES users(user_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_session_app
        FOREIGN KEY (app_id)
        REFERENCES applications(app_id)
        ON DELETE CASCADE
);

-- =========================================
-- 5. SYSTEM METRICS
-- =========================================
CREATE TABLE system_metrics (
    metric_id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT,
    cpu_usage FLOAT,
    ram_usage FLOAT,
    gpu_usage FLOAT,
    temperature FLOAT,
    recorded_at DATETIME DEFAULT GETDATE(),
    CONSTRAINT fk_metric_user
        FOREIGN KEY (user_id)
        REFERENCES users(user_id)
        ON DELETE CASCADE
);

-- =========================================
-- 6. APP RESOURCE USAGE
-- =========================================
CREATE TABLE app_resource_usage (
    resource_id INT IDENTITY(1,1) PRIMARY KEY,
    session_id INT,
    cpu_usage FLOAT,
    ram_usage FLOAT,
    gpu_usage FLOAT,
    recorded_at DATETIME DEFAULT GETDATE(),
    CONSTRAINT fk_resource_session
        FOREIGN KEY (session_id)
        REFERENCES sessions(session_id)
        ON DELETE CASCADE
);

-- =========================================
-- 7. INDEXES (OPTIMIZATION)
-- =========================================

-- sessions
CREATE INDEX idx_sessions_user ON sessions(user_id);
CREATE INDEX idx_sessions_app ON sessions(app_id);
CREATE INDEX idx_sessions_time ON sessions(start_time);

-- system_metrics
CREATE INDEX idx_metrics_user ON system_metrics(user_id);
CREATE INDEX idx_metrics_time ON system_metrics(recorded_at);

-- app_resource_usage
CREATE INDEX idx_resource_session ON app_resource_usage(session_id);
CREATE INDEX idx_resource_time ON app_resource_usage(recorded_at);

-- applications
CREATE INDEX idx_app_category ON applications(category_id);