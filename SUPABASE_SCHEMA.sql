-- Complete Supabase Database Schema

-- Enable Row Level Security (if tables exist)
ALTER TABLE IF EXISTS user_snapshots ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS user_favorites ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS user_preferences ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS shared_meal_plans ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS meal_plan_likes ENABLE ROW LEVEL SECURITY;


-- ORIGINAL TABLES


-- User Snapshots table
CREATE TABLE IF NOT EXISTS user_snapshots (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    date TEXT NOT NULL,
    user_preferences_json TEXT NOT NULL,
    meal_plan_xml TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(user_id, date)
);

-- User Favorites table
CREATE TABLE IF NOT EXISTS user_favorites (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    meal_plan_xml TEXT NOT NULL,
    meal_plan_hash TEXT NOT NULL,
    name TEXT NOT NULL,
    date TEXT NOT NULL,
    total_calories REAL NOT NULL,
    total_protein REAL NOT NULL,
    total_fat REAL NOT NULL,
    total_carbohydrates REAL NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(user_id, meal_plan_hash)
);

-- User Preferences table
CREATE TABLE IF NOT EXISTS user_preferences (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE UNIQUE,
    preferences_json TEXT NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);


-- NEW TABLES FOR DISCOVER FEATURE


-- Shared Meal Plans table
CREATE TABLE IF NOT EXISTS shared_meal_plans (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    user_email TEXT,
    meal_plan_xml TEXT NOT NULL,
    meal_plan_hash TEXT NOT NULL,
    name TEXT NOT NULL,
    date TEXT,
    total_calories REAL,
    total_protein REAL,
    total_fat REAL,
    total_carbohydrates REAL,
    likes_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(user_id, meal_plan_hash)
);

-- Meal Plan Likes table
CREATE TABLE IF NOT EXISTS meal_plan_likes (
    id BIGSERIAL PRIMARY KEY,
    meal_plan_id BIGINT NOT NULL REFERENCES shared_meal_plans(id) ON DELETE CASCADE,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(meal_plan_id, user_id)
);


-- ROW LEVEL SECURITY POLICIES - ORIGINAL TABLES


-- Snapshots Policies
DROP POLICY IF EXISTS "Users can read their own snapshots" ON user_snapshots;
CREATE POLICY "Users can read their own snapshots"
    ON user_snapshots FOR SELECT
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can insert their own snapshots" ON user_snapshots;
CREATE POLICY "Users can insert their own snapshots"
    ON user_snapshots FOR INSERT
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can update their own snapshots" ON user_snapshots;
CREATE POLICY "Users can update their own snapshots"
    ON user_snapshots FOR UPDATE
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can delete their own snapshots" ON user_snapshots;
CREATE POLICY "Users can delete their own snapshots"
    ON user_snapshots FOR DELETE
    USING (auth.uid() = user_id);

-- Favorites Policies
DROP POLICY IF EXISTS "Users can read their own favorites" ON user_favorites;
CREATE POLICY "Users can read their own favorites"
    ON user_favorites FOR SELECT
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can insert their own favorites" ON user_favorites;
CREATE POLICY "Users can insert their own favorites"
    ON user_favorites FOR INSERT
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can update their own favorites" ON user_favorites;
CREATE POLICY "Users can update their own favorites"
    ON user_favorites FOR UPDATE
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can delete their own favorites" ON user_favorites;
CREATE POLICY "Users can delete their own favorites"
    ON user_favorites FOR DELETE
    USING (auth.uid() = user_id);

-- Preferences Policies
DROP POLICY IF EXISTS "Users can read their own preferences" ON user_preferences;
CREATE POLICY "Users can read their own preferences"
    ON user_preferences FOR SELECT
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can insert their own preferences" ON user_preferences;
CREATE POLICY "Users can insert their own preferences"
    ON user_preferences FOR INSERT
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "Users can update their own preferences" ON user_preferences;
CREATE POLICY "Users can update their own preferences"
    ON user_preferences FOR UPDATE
    USING (auth.uid() = user_id);


-- ROW LEVEL SECURITY POLICIES - DISCOVER FEATURE


-- Shared Meal Plans Policies
DROP POLICY IF EXISTS "Allow authenticated users to read shared meal plans" ON shared_meal_plans;
CREATE POLICY "Allow authenticated users to read shared meal plans"
    ON shared_meal_plans FOR SELECT
    TO authenticated
    USING (true);

DROP POLICY IF EXISTS "Allow users to insert their own meal plans" ON shared_meal_plans;
CREATE POLICY "Allow users to insert their own meal plans"
    ON shared_meal_plans FOR INSERT
    TO authenticated
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "Allow users to update their own meal plans" ON shared_meal_plans;
CREATE POLICY "Allow users to update their own meal plans"
    ON shared_meal_plans FOR UPDATE
    TO authenticated
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "Allow users to delete their own meal plans" ON shared_meal_plans;
CREATE POLICY "Allow users to delete their own meal plans"
    ON shared_meal_plans FOR DELETE
    TO authenticated
    USING (auth.uid() = user_id);

-- Meal Plan Likes Policies
DROP POLICY IF EXISTS "Allow authenticated users to read likes" ON meal_plan_likes;
CREATE POLICY "Allow authenticated users to read likes"
    ON meal_plan_likes FOR SELECT
    TO authenticated
    USING (true);

DROP POLICY IF EXISTS "Allow users to like meal plans" ON meal_plan_likes;
CREATE POLICY "Allow users to like meal plans"
    ON meal_plan_likes FOR INSERT
    TO authenticated
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "Allow users to unlike meal plans" ON meal_plan_likes;
CREATE POLICY "Allow users to unlike meal plans"
    ON meal_plan_likes FOR DELETE
    TO authenticated
    USING (auth.uid() = user_id);


-- INDEXES FOR PERFORMANCE


-- Original table indexes
CREATE INDEX IF NOT EXISTS idx_user_snapshots_user_date ON user_snapshots(user_id, date);
CREATE INDEX IF NOT EXISTS idx_user_favorites_user ON user_favorites(user_id);
CREATE INDEX IF NOT EXISTS idx_user_preferences_user ON user_preferences(user_id);

-- New table indexes for Discover feature
CREATE INDEX IF NOT EXISTS idx_shared_meal_plans_user_id ON shared_meal_plans(user_id);
CREATE INDEX IF NOT EXISTS idx_shared_meal_plans_likes_count ON shared_meal_plans(likes_count DESC);
CREATE INDEX IF NOT EXISTS idx_shared_meal_plans_created_at ON shared_meal_plans(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_meal_plan_likes_meal_plan_id ON meal_plan_likes(meal_plan_id);
CREATE INDEX IF NOT EXISTS idx_meal_plan_likes_user_id ON meal_plan_likes(user_id);


-- ENABLE ROW LEVEL SECURITY (Final confirmation)


ALTER TABLE user_snapshots ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_favorites ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_preferences ENABLE ROW LEVEL SECURITY;
ALTER TABLE shared_meal_plans ENABLE ROW LEVEL SECURITY;
ALTER TABLE meal_plan_likes ENABLE ROW LEVEL SECURITY;
