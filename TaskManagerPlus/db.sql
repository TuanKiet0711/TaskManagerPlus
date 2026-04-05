-- =========================================
-- DATABASE
-- =========================================
CREATE DATABASE IF NOT EXISTS app_usage_tracker
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;

USE app_usage_tracker;

-- =========================================
-- 1. USERS
-- =========================================
CREATE TABLE users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    computer_name VARCHAR(100),
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- =========================================
-- 2. CATEGORIES
-- =========================================
CREATE TABLE categories (
    category_id INT AUTO_INCREMENT PRIMARY KEY,
    category_name VARCHAR(50) NOT NULL UNIQUE
) ENGINE=InnoDB;

-- =========================================
-- 3. APPLICATIONS
-- =========================================
CREATE TABLE applications (
    app_id INT AUTO_INCREMENT PRIMARY KEY,
    app_name VARCHAR(100) NOT NULL,
    exe_name VARCHAR(100),
    category_id INT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_app_category
        FOREIGN KEY (category_id)
        REFERENCES categories(category_id)
        ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE INDEX idx_app_category ON applications(category_id);

-- =========================================
-- 4. SESSIONS (CORE TABLE)
-- =========================================
CREATE TABLE sessions (
    session_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    app_id INT NOT NULL,
    process_id INT NOT NULL,
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
) ENGINE=InnoDB;

-- Indexes
CREATE INDEX idx_sessions_user_app_time
ON sessions(user_id, app_id, start_time DESC);

CREATE INDEX idx_sessions_user_process_time
ON sessions(user_id, process_id, start_time DESC);

CREATE INDEX idx_sessions_app_time
ON sessions(app_id, start_time DESC);

CREATE INDEX idx_sessions_time
ON sessions(start_time);

-- =========================================
-- 5. SYSTEM METRICS (HIGH-FREQUENCY DATA)
-- =========================================
CREATE TABLE system_metrics (
    metric_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,

    cpu_usage DECIMAL(5,2) NOT NULL,
    ram_usage DECIMAL(5,2) NOT NULL,
    gpu_usage DECIMAL(5,2),
    temperature DECIMAL(5,2),

    recorded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_metric_user
        FOREIGN KEY (user_id)
        REFERENCES users(user_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX idx_metrics_user_time
ON system_metrics(user_id, recorded_at DESC);

-- =========================================
-- 6. APP RESOURCE USAGE
-- =========================================
CREATE TABLE app_resource_usage (
    resource_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    session_id BIGINT NOT NULL,

    cpu_usage DECIMAL(5,2),
    ram_usage BIGINT,
    gpu_usage DECIMAL(5,2),

    recorded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_resource_session
        FOREIGN KEY (session_id)
        REFERENCES sessions(session_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX idx_resource_session_time
ON app_resource_usage(session_id, recorded_at DESC);
