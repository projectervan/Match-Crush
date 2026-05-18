# Enhancement 2 — Monetization, Retention & Customization

---

## 1. Sistem "Near Miss" & Ekonomi Dalam Game

Pemain paling merasa tertantang ketika mereka hampir menang, tetapi langkah (moves) habis. Kita bisa memonetisasi rasa penasaran ini.

### Sistem Mata Uang (Coins)

Tambahkan koin virtual. Pemain mendapatkan sedikit koin saat menyelesaikan level.

### Fitur "+5 Moves" (Penyelamat)

Saat Game Over karena langkah habis, jangan langsung tutup permainannya. Munculkan pop-up:

> "Out of moves! Buy +5 Moves for 50 Coins?"

Ini menciptakan siklus "satu tarikan napas lagi" yang sangat adiktif.

### Pre-Game Boosters

Sebelum level dimulai, izinkan pemain membeli power-up menggunakan koin (misal: mulai game langsung dengan 1 Bom atau 1 Color Bomb di papan).

---

## 2. Sistem Nyawa (Energy / Lives)

Ini adalah trik psikologis tertua di game mobile. Jangan biarkan pemain bermain terus-menerus tanpa batas saat mereka kalah; buat mereka "merindukan" game Anda.

### Maksimal 5 Nyawa (Hearts)

Pemain hanya kehilangan nyawa jika mereka gagal/kalah di suatu level. Jika menang, nyawa tidak berkurang.

### Regenerasi Berbasis Waktu

Jika nyawa kurang dari 5, isi ulang 1 nyawa setiap 30 menit. Karena Anda menggunakan Firebase, Anda bisa menyimpan timestamp terakhir kali nyawa berkurang, sehingga hitungan mundur tetap berjalan meskipun game ditutup.

---

## 3. Retensi Harian (Daily Habits)

Buat alasan kuat agar pemain membuka aplikasi Anda setidaknya satu kali sehari.

### Daily Login Reward (Streak)

Berikan hadiah yang semakin membesar jika pemain login berturut-turut:

| Hari | Hadiah |
|------|--------|
| Hari 1 | 50 Koin |
| Hari 2 | 100 Koin |
| Hari 7 | Pre-Game Booster Spesial |

Jika terlewat satu hari, kembali ke Hari 1.

### Daily Quests (Misi Harian)

Tambahkan 3 misi acak setiap hari yang memberikan koin ekstra.

**Contoh:**
- "Hancurkan 500 Candy Merah"
- "Gunakan 5 Bom dalam 1 hari"
- "Selesaikan 3 Level tanpa kalah"

---

## 4. Kosmetik & Kustomisasi (Long-Term Meta)

Karena level Anda unlimited (prosedural), pemain butuh "Tujuan Akhir" (Long-term goal).

### Unlockable Themes

Buat toko (Shop) di mana pemain bisa menukar koin atau menyelesaikan level tertentu untuk membuka:

- **Background baru** — misal: Tema Hutan, Tema Luar Angkasa
- **Skin untuk objek candy** — misal: bentuk permata, bentuk buah
