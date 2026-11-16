// YCC TEAM FACEIT aracısı (Cloudflare Worker, 2026-11-16). FACEIT sunucu firmalarının IP'lerini engellediği için oyun sunucusu
// oyuncunun seviyesini bunun üzerinden sorar. Sadece "X-Ycc" başlığında doğru şifreyi gönderen cevap alır; FACEIT anahtarı
// Cloudflare'de gizli değişken olarak durur (FACEIT_ANAHTAR), şifre de (YCC_SIFRE). Sadece oyuncu bilgisi sorgusu geçer.
export default {
  async fetch(istek, env) {
    if (istek.headers.get("x-ycc") !== env.YCC_SIFRE) return new Response("yasak", { status: 403 });
    const sid = new URL(istek.url).searchParams.get("game_player_id") || "";
    if (!/^7656119\d{10}$/.test(sid)) return new Response("hatali", { status: 400 });
    const r = await fetch(`https://open.faceit.com/data/v4/players?game=cs2&game_player_id=${sid}`, {
      headers: { Authorization: `Bearer ${env.FACEIT_ANAHTAR}`, Accept: "application/json", "User-Agent": "ycc-faceit/1.0" },
    });
    return new Response(await r.text(), { status: r.status, headers: { "content-type": "application/json" } });
  },
};
