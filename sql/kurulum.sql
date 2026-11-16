-- YccSkin veritabanı tabloları (YCC TEAM sunucusundaki yapının aynısı, içinde veri yok).
-- Kullanım: mysql -u cs2 -p cs2 < sql/kurulum.sql   (tablo zaten varsa dokunulmaz)

CREATE TABLE IF NOT EXISTS `wp_player_skins` (
  `steamid` varchar(18) NOT NULL,
  `weapon_team` int(11) NOT NULL,
  `weapon_defindex` int(11) NOT NULL,
  `weapon_paint_id` int(11) NOT NULL,
  `weapon_wear` float NOT NULL DEFAULT 0.000001,
  `weapon_seed` int(11) NOT NULL DEFAULT 0,
  `weapon_nametag` varchar(128) DEFAULT NULL,
  `weapon_stattrak` tinyint(1) NOT NULL DEFAULT 0,
  `weapon_stattrak_count` int(11) NOT NULL DEFAULT 0,
  `weapon_sticker_0` varchar(128) NOT NULL DEFAULT '0;0;0;0;0;0;0' COMMENT 'id;schema;x;y;wear;scale;rotation',
  `weapon_sticker_1` varchar(128) NOT NULL DEFAULT '0;0;0;0;0;0;0' COMMENT 'id;schema;x;y;wear;scale;rotation',
  `weapon_sticker_2` varchar(128) NOT NULL DEFAULT '0;0;0;0;0;0;0' COMMENT 'id;schema;x;y;wear;scale;rotation',
  `weapon_sticker_3` varchar(128) NOT NULL DEFAULT '0;0;0;0;0;0;0' COMMENT 'id;schema;x;y;wear;scale;rotation',
  `weapon_sticker_4` varchar(128) NOT NULL DEFAULT '0;0;0;0;0;0;0' COMMENT 'id;schema;x;y;wear;scale;rotation',
  `weapon_keychain` varchar(128) NOT NULL DEFAULT '0;0;0;0;0' COMMENT 'id;x;y;z;seed',
  UNIQUE KEY `steamid` (`steamid`,`weapon_team`,`weapon_defindex`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `wp_player_knife` (
  `steamid` varchar(18) NOT NULL,
  `weapon_team` int(11) NOT NULL,
  `knife` varchar(64) NOT NULL,
  UNIQUE KEY `steamid` (`steamid`,`weapon_team`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `wp_player_gloves` (
  `steamid` varchar(18) NOT NULL,
  `weapon_team` int(11) NOT NULL,
  `weapon_defindex` int(11) NOT NULL,
  UNIQUE KEY `steamid` (`steamid`,`weapon_team`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `wp_player_agents` (
  `steamid` varchar(18) NOT NULL,
  `agent_ct` varchar(64) DEFAULT NULL,
  `agent_t` varchar(64) DEFAULT NULL,
  UNIQUE KEY `steamid` (`steamid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `wp_player_music` (
  `steamid` varchar(64) NOT NULL,
  `weapon_team` int(11) NOT NULL,
  `music_id` int(11) NOT NULL,
  UNIQUE KEY `steamid` (`steamid`,`weapon_team`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `wp_player_pins` (
  `steamid` varchar(64) NOT NULL,
  `weapon_team` int(11) NOT NULL,
  `id` int(11) NOT NULL,
  UNIQUE KEY `steamid` (`steamid`,`weapon_team`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
