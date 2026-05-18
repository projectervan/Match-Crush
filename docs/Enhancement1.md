# Enhancement 1 — Gameplay, Visual & Meta-Game Improvements

---

## 1. Penambahan Mekanik Gameplay (The Hook)

Pemain cepat bosan jika hanya mencocokkan 3 objek. Anda perlu menambahkan elemen kejutan dan strategi.

### Sistem Power-Up (Special Candies)

- **Match-4:** Menciptakan objek yang jika dicocokkan akan menghancurkan satu baris penuh (Horizontal/Vertical Blast).
- **Match-5 (T/L Shape):** Menciptakan objek bom yang meledak dalam radius 3x3.
- **Match-5 (Garis Lurus):** Menciptakan "Color Bomb" yang menghancurkan semua objek dengan warna tertentu di papan.

### Sistem Cascade & Multiplier

Saat terjadi efek beruntun (chain reaction di mana objek jatuh dan otomatis membentuk match baru), berikan multiplier skor (x2, x3) agar pemain merasa pintar dan beruntung.

### Rintangan Dinamis (Obstacles)

Alih-alih hanya mengejar skor, ubah target beberapa level menjadi menghancurkan "Ice/Jelly" yang berada di belakang objek, atau objek terkunci yang tidak bisa digeser.

---

## 2. Peningkatan Visual & Game Feel (The Juice)

UI dan efek visual adalah kunci utama kepuasan bermain (dopamine hit).

### Screen Shake & Haptic Feedback

Tambahkan getaran layar kecil (Screen Shake) dan getaran HP (menggunakan API Vibrate Android) saat terjadi ledakan besar atau combo.

### Efek Partikel (Particle System)

Saat objek hancur, jangan langsung hilangkan. Buat partikel percikan warna-warni menggunakan Unity Particle System.

### Pujian Visual (Combo Pop-ups)

Munculkan teks animasi seperti "Sweet!", "Awesome!", atau "Unbelievable!" saat pemain melakukan match lebih dari 3 atau mendapatkan combo beruntun.

### Animasi Squash & Stretch

Saat objek jatuh dari atas, berikan sedikit efek memantul (bouncy) saat mendarat agar papan terasa dinamis, bukan kaku.

---

## 3. Peningkatan Meta-Game & UI (The Habit)

Karena Anda sudah menggunakan Firebase, Anda memiliki keunggulan untuk fitur online.

### Global Leaderboard

Tambahkan satu tombol di Main Menu untuk melihat Top 50 pemain dengan High Score tertinggi. Ini akan memicu insting kompetitif.

### Tensi di Akhir Permainan

Saat Moves (langkah) tersisa 5 atau kurang, ubah warna UI sisa langkah menjadi merah dan berikan efek suara detak jantung untuk menciptakan tensi.

### Map Progression Sederhana

Daripada hanya tombol "Next Level", buat UI layar yang menunjukkan rute level (Level 1 → Level 2 → Level 3) agar pemain merasa sedang dalam sebuah perjalanan.
