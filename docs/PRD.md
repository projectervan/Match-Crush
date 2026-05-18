# Product Requirements Document (PRD)

**Project Name:** 2D Infinite Match-3 Puzzle (Android)  
**Target Platform:** Android (Google Play Store)  
**Game Engine:** Unity LTS (C#)  
**Backend:** Firebase SDK for Unity (Auth & Firestore)

---

## 1. Core Architecture & Game Design

- **Grid System:** Papan berukuran statis 8x8.
- **Gameplay Core:** Match-3 tradisional. Menukar (swipe) 2 candy/gem yang bersebelahan.
- **Constraint:** Pemain dibatasi oleh jumlah langkah (Move Limit) per level, bukan waktu.
- **Progression:** Procedural / Unlimited Levels. Tingkat kesulitan (Target Skor dan Batas Langkah) dihitung secara matematis berdasarkan level saat ini.

---

## 2. Technical Stack & Dependencies

AI Agent harus menggunakan package/library berikut di Unity:

- **Unity 2D Core:** Sprites, 2D Physics (jika diperlukan), UI Canvas.
- **Firebase SDK for Unity:**
  - `FirebaseAuthentication.unitypackage` (Google Sign-In)
  - `FirebaseFirestore.unitypackage` (Data storage)
- **Google Sign-In Unity Plugin:** Untuk memunculkan native prompt login Google di Android.
- **Design Pattern:** Rekomendasi menggunakan Singleton Pattern untuk Manager (`GameManager`, `UIManager`, `AuthManager`).

---

## 3. Core Mechanics & C# Scripting Requirements

AI Agent harus membuat dan menyusun script dengan tanggung jawab berikut:

### `GridManager.cs`

- **Fungsi:** Menginisialisasi grid array 2D berukuran 8x8 (`GameObject[,] board = new GameObject[8, 8]`).
- **Logika:** Mengisi grid kosong, mendeteksi match (horizontal/vertikal minimal 3), menghancurkan objek (`Destroy`), dan menurunkan objek di atasnya (Gravity Drop).

### `Piece.cs` atau `Candy.cs`

- **Fungsi:** Menempel pada setiap prefab objek di grid.
- **Logika:** Menangani input drag/swipe pemain menggunakan `IPointerDownHandler`, `IPointerUpHandler`, dan animasi pertukaran posisi (Lerp/Tweening).

### `GameManager.cs`

- **Fungsi:** Mengatur Game State (`Playing`, `Won`, `Lost`, `Paused`).
- **Logika Procedural:**
  - Menentukan `TargetScore = 1000 + (CurrentLevel * 500)`.
  - Menentukan `MoveLimit = Mathf.Max(10, 30 - (CurrentLevel / 5))`.
  - Mengurangi move setiap kali pemain melakukan swipe yang valid.
  - Mengecek kondisi menang jika skor tercapai sebelum/saat move habis.

### `AuthManager.cs` & `DatabaseManager.cs`

- **Fungsi:** Menangani inisialisasi Firebase, autentikasi Google, dan integrasi Firestore.

---

## 4. Database Schema (Firebase Firestore)

AI harus mengimplementasikan skema ini menggunakan `Firebase.Firestore`:

- **Collection:** `users`
- **Document ID:** `user.UserId` (dari Firebase Auth)
- **Data Structure (C# Dictionary/Class):**

```csharp
public class UserData {
    public string DisplayName { get; set; }
    public int CurrentLevel { get; set; } // Default: 1
    public int HighScore { get; set; }    // Default: 0
}
```

---

## 5. UI & Scene Flow (Unity Canvas)

AI Agent perlu membangun hierarki UI berikut dalam Unity:

### Scene 1: `LoginScene`

- **UI:** Background 2D, Tombol "Login with Google", Teks Status.
- **Flow:** Jika `AuthManager` mendeteksi user sudah login, langsung `SceneManager.LoadScene("MainMenu")`.

### Scene 2: `MainMenuScene`

- **UI:**
  - Teks `{DisplayName}`
  - Teks `Level: {CurrentLevel}`
  - Teks `High Score: {HighScore}` (semua di-fetch dari Firestore).
- **UI:** Tombol "PLAY" yang mengarah ke `GameplayScene`.

### Scene 3: `GameplayScene`

- **Top Bar HUD:**
  - Teks "Level X"
  - Teks "Score: 0 / Target"
  - Teks "Moves Left: Y"
- **Center:** Area papan 8x8 (didukung oleh `GridLayoutGroup` atau kalkulasi posisi lokal).
- **Modals/Panels (Hidden by default):**
  - **VictoryPanel:** Muncul saat menang. Mengandung animasi bintang/teks "Level Cleared", dan memicu `DatabaseManager` untuk `CurrentLevel++`. Tombol "Next Level".
  - **GameOverPanel:** Muncul saat `Moves == 0` dan skor belum tercapai. Tombol "Retry".
