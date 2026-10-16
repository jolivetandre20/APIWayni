-- One-time setup of a host MySQL 8.0+ (e.g. "Local instance MySQL80") for docker-compose.localmysql.yml.
-- Run it in MySQL Workbench connected as root. Replace <MYSQL_PASSWORD> with the MYSQL_PASSWORD of your .env
-- (do not save the real password in this file).

-- 1. Database (the sample backup also creates it; harmless if it already exists).
CREATE DATABASE IF NOT EXISTS wayni_deudores CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- 2. Application user. '%' because the containers connect through the Docker network gateway, not localhost.
CREATE USER IF NOT EXISTS 'wayni'@'%' IDENTIFIED BY '<MYSQL_PASSWORD>';
GRANT ALL PRIVILEGES ON wayni_deudores.* TO 'wayni'@'%';
FLUSH PRIVILEGES;

-- 3. The Processor bulk-loads ~20 million rows with LOAD DATA LOCAL INFILE (persists across restarts).
SET PERSIST local_infile = 1;

-- Checks
SELECT VERSION() AS mysql_version, @@local_infile AS local_infile;   -- needs 8.0.19+ and 1
SHOW GRANTS FOR 'wayni'@'%';
