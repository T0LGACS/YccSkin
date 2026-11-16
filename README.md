# YccSkin

Skin sistemi: silah skinleri, bıçak, eldiven, ajan, müzik kiti, rozet ve StatTrak (öldürdükçe artar); çıkartma ve anahtarlıklar da uygulanır. Seçimler veritabanında, her girişte geri gelir. Menüler W/S/E/R ile gezilir. Skin listesi oyunun kendi dosyalarından üretilir.

> Skin changer: weapon skins, knives, gloves, agents, music kits, pins and working StatTrak counters. Skin list is generated from the game's own files.

[YCC TEAM](https://yccteam.com.tr) CS2 sunucusu için sıfırdan yazıldı ve sunucuda her gün çalışıyor. Başka eklentilerden kod alınmadı.

## Gereken

- CS2 sunucusu, [Metamod:Source](https://www.sourcemm.net/) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) (1.0.371 ve üstü)
- MySQL / MariaDB veritabanı
- Python 3 (sadece araçlar için)
- FACEIT rozetleri için (isteğe bağlı): FACEIT API anahtarı, ücretsiz Cloudflare hesabı ve MultiAddonManager

## Kurulum

1. Sunucuda [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) (with-runtime sürümü) kurulu olmalı.
2. [.NET 10 SDK](https://dotnet.microsoft.com/download) ile derle: `dotnet build -c Release`
3. `bin/Release/net10.0/` içindeki her şeyi sunucuda `addons/counterstrikesharp/plugins/YccSkin/` klasörüne kopyala.
4. Sunucuyu başlat. Ayar dosyası ilk açılışta `addons/counterstrikesharp/configs/plugins/YccSkin/YccSkin.json` olarak kendiliğinden oluşur; düzenleyip sunucuyu yeniden başlat (önce aşağıdaki **Veritabanı** bölümünü yap).

## Komutlar

Sohbette `!` ile, konsolda `css_` ile yazılır.

| Komut | Ne yapar |
|---|---|
| `!ws / !skin` | Silah skini (elindeki silah en üstte) |
| `!knife` | Bıçak |
| `!eldiven / !glove` | Eldiven |
| `!ajan / !agent` | Ajan |
| `!music` | Müzik kiti |
| `!pin / !coin` | Rozet (en üstte FACEIT seviyem) |
| `!st / !stattrak` | Elindeki silahta StatTrak aç / kapa |
| `!wp` | Skinleri yeniler |
| `css_skinyenile <steamid64>` | Sunucu konsolu: oyuncunun seçimlerini veritabanından yeniden yükler (web paneli için) |

## Notlar

`arac/` klasöründeki kendi araçlarımız: `vpk.py` oyunun paket dosyasını okur, `veri_uret.py` skin listesini (`data/skinler.json`) üretir, `imza_bul.py` CS2 güncellemesinden sonra skin fonksiyonunun yeni imzasını bulur, `skin_kontrol.py` bu ikisini güncellemeden sonra otomatik çalıştırır.

## Veritabanı (MySQL / MariaDB) kurulumu

Bu eklenti kayıtlarını veritabanında tutar. Eklenti tabloları kendisi **oluşturmaz**; depodaki `sql/kurulum.sql` dosyası oluşturur.
Kullandığı tablolar: wp_player_skins, wp_player_knife, wp_player_gloves, wp_player_agents, wp_player_music, wp_player_pins. YccAdmin, YccRank ve YccSkin aynı veritabanını paylaşabilir.

**1. MariaDB'yi kur** (Ubuntu / Debian; veritabanın zaten varsa bu adımı geç):

```bash
sudo apt update && sudo apt install -y mariadb-server
sudo systemctl enable --now mariadb
```

**2. Veritabanı ve kullanıcı oluştur.** `sudo mysql` ile gir ve şunları yaz (şifreyi kendin belirle, kimseyle paylaşma):

```sql
CREATE DATABASE IF NOT EXISTS cs2 CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
CREATE USER 'cs2'@'127.0.0.1' IDENTIFIED BY 'BURAYA-GUCLU-BIR-SIFRE';
CREATE USER 'cs2'@'localhost' IDENTIFIED BY 'BURAYA-GUCLU-BIR-SIFRE';
GRANT SELECT, INSERT, UPDATE, DELETE ON cs2.* TO 'cs2'@'127.0.0.1', 'cs2'@'localhost';
FLUSH PRIVILEGES;
```

Eklentiye okuma/yazma yetkisi yeter; tabloları sen kuruyorsun (3. adım).

**3. Tabloları kur** (depo klasöründeyken):

```bash
sudo mysql cs2 < sql/kurulum.sql
```

Tablo zaten varsa dokunulmaz (`CREATE TABLE IF NOT EXISTS`), mevcut verin silinmez.

**4. Ayar dosyasına bağlantıyı yaz.** Sunucu bir kez açılınca `addons/counterstrikesharp/configs/plugins/YccSkin/YccSkin.json` oluşur; içindeki `Veritabani` bölümünü doldur:

```json
"Veritabani": {
  "Sunucu": "127.0.0.1",
  "Port": 3306,
  "Kullanici": "cs2",
  "Sifre": "BURAYA-GUCLU-BIR-SIFRE",
  "Ad": "cs2"
}
```

Sonra CS2 sunucusunu yeniden başlat. Bağlanamazsa sunucu konsolunda `[YccSkin]` ile başlayan bir hata satırı çıkar.

**Güvenlik:** 3306 portunu internete açma; veritabanı oyun sunucusuyla aynı makinedeyse `127.0.0.1` yeterli. Başka bir makinedeyse
kullanıcıyı o makinenin IP'siyle oluştur (`'cs2'@'1.2.3.4'`) ve güvenlik duvarında sadece o IP'ye izin ver. Ayar dosyası şifre içerir:
kimseyle paylaşma, GitHub'a koyma.

## FACEIT seviye rozetleri (isteğe bağlı)

Oyuncu sunucuya ilk girdiğinde FACEIT seviyesi sorulur ve seviyesine göre bir rozet (pin) takılır; TAB'da isminin yanında görünür.
FACEIT rozetleri elle seçilemez: `!pin` menüsünde (ve web panelinde) yerlerine tek bir **"FACEIT seviyem (otomatik)"** seçeneği var;
seçilince seviye yeniden sorulur ve oyuncunun kendi seviyesinin rozeti takılır. FACEIT hesabı olmayana rozet takılmaz.

Seviye rozetleri oyunda olmadığı için kullanılmayan 10 hizmet madalyasının (2016 lvl1-6 ve 2017 lvl1-4, yani 1339-1344 ve 1357-1360)
resmi bir Workshop eklentisiyle seviye ikonuna çevrilir. `FaceitAdres` boşken bu özellik tamamen kapalıdır.

### 1. FACEIT API anahtarı al

1. <https://developers.faceit.com> adresine git ve sağ üstten **FACEIT hesabınla giriş yap** (hesabın yoksa ücretsiz aç).
2. **App Studio**'ya gir ve **Create App** ile yeni bir uygulama oluştur (ad ve açıklama serbest, örneğin "CS2 sunucum").
3. Uygulamanın sayfasında **API Keys** sekmesine geç, **Create** de ve türü **Server side** seç.
4. Çıkan anahtarı kopyala. Bu anahtar **gizlidir**: kimseyle paylaşma, Cloudflare'deki gizli değişken (ya da ayar dosyası) dışında hiçbir yere yazma.

Eklentinin yaptığı tek istek: `GET https://open.faceit.com/data/v4/players?game=cs2&game_player_id=<SteamID64>` (seviye `games.cs2.skill_level` alanında).

### 2. Aracı kur (Cloudflare Worker, ücretsiz)

FACEIT'in API'si Cloudflare arkasında ve sunucu firmalarının (VDS / VPS) IP'lerine çoğu zaman "Just a moment" engeli koyuyor; oyun sunucusu
doğrudan soramıyor. Bu yüzden istek ücretsiz bir Cloudflare Worker üzerinden gider. FACEIT anahtarı Worker'da gizli değişken olarak durur;
oyun sunucusuna sadece Worker'ın adresi ve senin belirlediğin bir şifre yazılır. Kod bu depoda: `faceit-araci/worker.js`.

1. <https://dash.cloudflare.com> → **Workers & Pages** → **Create** → **Start with Hello World!** → adını `ycc-faceit` (ya da istediğin bir ad) yap → **Deploy**.
   "Protect with Cloudflare Access" **kapalı** kalsın; açarsan oyun sunucusu Worker'a ulaşamaz.
2. **Edit code** → içindekini sil, `faceit-araci/worker.js`'in içeriğini yapıştır → **Deploy**.
3. **Settings → Variables and Secrets → Add** ile iki tane **Secret** ekle (Key = ad, Value = değer):
   - `FACEIT_ANAHTAR` → 1. adımdaki FACEIT anahtarın
   - `YCC_SIFRE` → uzun rastgele bir şifre (örneğin `openssl rand -hex 18` çıktısı). Oyun sunucusu bu şifreyi `X-Ycc` başlığında gönderir;
     şifresiz gelen isteğe Worker "yasak" der, böylece adresini öğrenen biri FACEIT anahtarını kullanamaz.
4. **Bölge ayarı (önemli):** Worker, isteğin geldiği yere en yakın Cloudflare merkezinde çalışır; bazı merkezlerden (örneğin İstanbul) FACEIT'e
   giden istek de engelleniyor. `faceit-araci/wrangler.toml` Worker'ı Frankfurt'ta çalıştırır. Node.js kurulu bir bilgisayarda:
   ```bash
   cd faceit-araci
   npx wrangler login     # tarayıcıda Cloudflare izin sayfası açılır, Allow de
   npx wrangler deploy    # Worker'ı Frankfurt ayarıyla yükler, gizli değişkenlere dokunmaz
   npx wrangler logout    # bilgisayarındaki Cloudflare yetkisini geri al
   ```
   Worker'a farklı bir ad verdiysen `wrangler.toml`'daki `name` satırını da aynı yap.
5. Dene (oyun sunucusunda çalıştır; 200 ve oyuncu bilgisi dönmeli):
   ```bash
   curl -H "x-ycc: SENIN-SIFREN" "https://ycc-faceit.HESABIN.workers.dev/?game_player_id=76561197960287930"
   ```
   403 ve "Just a moment" dönerse 4. adımı yap; "yasak" dönerse şifre tutmuyordur.

### 3. YccSkin ayarı

`addons/counterstrikesharp/configs/plugins/YccSkin/YccSkin.json` içine yaz, sonra sunucuyu yeniden başlat:

```json
"FaceitAdres": "https://ycc-faceit.HESABIN.workers.dev/",
"FaceitAraciSifre": "SENIN-SIFREN",
"FaceitAnahtari": "",
"FaceitRozetleri": [1339, 1340, 1341, 1342, 1343, 1344, 1357, 1358, 1359, 1360]
```

- `FaceitRozetleri`: seviye 1..10 için takılacak rozet numaraları (sırası önemli).
- Aracı kullanmadan doğrudan sormak istersen (sunucunun IP'si FACEIT'te engelli değilse):
  `"FaceitAdres": "https://open.faceit.com/data/v4/players"`, `"FaceitAnahtari": "<FACEIT anahtarın>"`, `"FaceitAraciSifre": ""`.

### 4. Rozet resimleri (Workshop eklentisi)

Seviye ikonlarını gösteren Workshop eklentimiz herkese açık: **3840799699**. Sunucuya
[MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager) kur ve `cfg/multiaddonmanager/multiaddonmanager.cfg` içine yaz:

```
mm_extra_addons "3840799699"
```

Oyuncular sunucuya girerken paketi (~700 KB) bir kez indirir. Kendi ikonlarını yapmak istersen `rozet/ciz.py` (Python + Pillow) seviye resimlerini çizer.

**Dikkat:**
- Yeni yüklenen ya da güncellenen Workshop öğesi Steam moderasyonu onaylayana kadar başkalarına inmez. Onaysız öğeyi `mm_extra_addons`'a
  eklersen sunucu "Access is denied" alır ve MultiAddonManager haritayı durmadan yeniden yükler. Önce onayı bekle.
- CS2 Workshop Tools paketleyicisi `panorama/images/econ` klasörünü pakete almıyor; almasını sağlamak için oyunun kendi dosyasını
  (`gameinfo.gi`) değiştirmek gerekiyor. Değiştirilmiş dosyayla oyun "güvensiz mod"a geçer ve VAC'lı sunuculara giremezsin. Bu yüzden hazır
  Workshop öğemizi kullanmanı öneririz; kendin yapacaksan iş bitince Steam'den **oyun dosyalarının bütünlüğünü doğrula**.

## Lisans

MIT. Kullanabilir, değiştirebilir, paylaşabilirsin; lisans dosyasını koruman yeterli.

Sunucu: **yccteam.com.tr** · Site: https://yccteam.com.tr
