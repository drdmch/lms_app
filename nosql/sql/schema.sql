-- Частина бази з лаби 1 (контент курсу), яку я виношу в MongoDB.
-- Таблиці ті самі, що в lms_db, але без аудиту і soft delete.
-- Нове: lesson_tags, у materials додано duration_sec (для відео) і body (для статей).

CREATE TABLE categories (
    id   SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL
);

CREATE TABLE courses (
    id          SERIAL PRIMARY KEY,
    category_id INT NOT NULL REFERENCES categories(id),
    title       VARCHAR(200) NOT NULL
);

CREATE TABLE modules (
    id        SERIAL PRIMARY KEY,
    course_id INT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    title     VARCHAR(200) NOT NULL,
    position  INT NOT NULL
);

CREATE TABLE lessons (
    id        SERIAL PRIMARY KEY,
    module_id INT NOT NULL REFERENCES modules(id) ON DELETE CASCADE,
    title     VARCHAR(200) NOT NULL,
    position  INT NOT NULL
);

CREATE TABLE lesson_tags (
    lesson_id INT NOT NULL REFERENCES lessons(id) ON DELETE CASCADE,
    tag       VARCHAR(50) NOT NULL,
    PRIMARY KEY (lesson_id, tag)
);

CREATE TABLE materials (
    id           SERIAL PRIMARY KEY,
    lesson_id    INT NOT NULL REFERENCES lessons(id) ON DELETE CASCADE,
    type         VARCHAR(20) NOT NULL,   -- video, pdf, link, article
    url          VARCHAR(500) NOT NULL,
    duration_sec INT,                    -- тільки для video
    body         TEXT                    -- тільки для article
);

CREATE TABLE quizzes (
    id         SERIAL PRIMARY KEY,
    lesson_id  INT NOT NULL REFERENCES lessons(id) ON DELETE CASCADE,
    title      VARCHAR(200) NOT NULL,
    pass_score INT NOT NULL
);

CREATE TABLE questions (
    id      SERIAL PRIMARY KEY,
    quiz_id INT NOT NULL REFERENCES quizzes(id) ON DELETE CASCADE,
    text    TEXT NOT NULL
);

CREATE TABLE answer_options (
    id          SERIAL PRIMARY KEY,
    question_id INT NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    text        VARCHAR(500) NOT NULL,
    is_correct  BOOLEAN NOT NULL DEFAULT false
);

-- індекси на зовнішні ключі (як у лабі 1) + на тег для пошуку
CREATE INDEX ix_modules_course    ON modules(course_id);
CREATE INDEX ix_lessons_module    ON lessons(module_id);
CREATE INDEX ix_materials_lesson  ON materials(lesson_id);
CREATE INDEX ix_quizzes_lesson    ON quizzes(lesson_id);
CREATE INDEX ix_questions_quiz    ON questions(quiz_id);
CREATE INDEX ix_options_question  ON answer_options(question_id);
CREATE INDEX ix_lesson_tags_tag   ON lesson_tags(tag);

INSERT INTO categories(name) VALUES
('Програмування'), ('Бази даних'), ('Дизайн'), ('Математика'), ('Мови'),
('Маркетинг'), ('DevOps'), ('Фізика'), ('Менеджмент'), ('Безпека');
