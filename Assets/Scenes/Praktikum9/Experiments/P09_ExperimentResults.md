# Hasil Eksperimen Praktikum 9 - Smart Spawner

Dibuat 2026-11-01 21:06. Konfigurasi dasar: enemy 8, item 15, area 36 x 36 m, safe radius 5, min spacing 1.5, max attempts 30, bobot enemy 60:30:10.

`Gagal spawn` = objek yang dilewati karena maxAttempts habis. `Posisi ditolak` = jumlah posisi kandidat yang gagal validasi.


## 1. Perbandingan seed

| Variasi | Seed | Enemy | Item | Legendary | Gagal spawn | Posisi ditolak (player / exit / spacing / obstacle) | Jenis enemy | Jenis item | Layout hash |
|---|---|---|---|---|---|---|---|---|---|
| Seed 100 | 100 | 8/8 | 15/15 | 1 | 0 | 5 (1 / 2 / 1 / 1) | Grunt 6, Brute 1, Scout 1 | Coin 4, Potion 6, Key 4, Legendary 1 | `F5BEA89B` |
| Seed 200 | 200 | 8/8 | 15/15 | 1 | 0 | 11 (3 / 2 / 2 / 4) | Grunt 7, Brute 1, Scout 0 | Coin 9, Potion 4, Key 1, Legendary 1 | `3F4109B1` |
| Seed 300 | 300 | 8/8 | 15/15 | 1 | 0 | 10 (1 / 1 / 2 / 6) | Grunt 5, Brute 2, Scout 1 | Coin 8, Potion 5, Key 1, Legendary 1 | `85A72B68` |

## 2. Reproducibility (seed 500 x 3)

| Run | Seed | Enemy | Item | Legendary | Gagal spawn | Posisi ditolak (player / exit / spacing / obstacle) | Jenis enemy | Jenis item | Layout hash |
|---|---|---|---|---|---|---|---|---|---|
| Run 1 | 500 | 8/8 | 15/15 | 1 | 0 | 8 (1 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `62609F50` |
| Run 2 | 500 | 8/8 | 15/15 | 1 | 0 | 8 (1 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `62609F50` |
| Run 3 | 500 | 8/8 | 15/15 | 1 | 0 | 8 (1 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `62609F50` |

Semua run menghasilkan layout hash `62609F50` yang sama: **identik**.

## 3. Jarak aman Player (seed 500)

| Safe radius | Seed | Enemy | Item | Legendary | Gagal spawn | Posisi ditolak (player / exit / spacing / obstacle) | Jenis enemy | Jenis item | Layout hash |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 500 | 8/8 | 15/15 | 1 | 0 | 7 (0 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `087679C9` |
| 5 | 500 | 8/8 | 15/15 | 1 | 0 | 8 (1 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `62609F50` |
| 8 | 500 | 8/8 | 15/15 | 1 | 0 | 11 (4 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `C31CC9CA` |

## 4. Minimum spacing (seed 500)

| Min spacing | Seed | Enemy | Item | Legendary | Gagal spawn | Posisi ditolak (player / exit / spacing / obstacle) | Jenis enemy | Jenis item | Layout hash |
|---|---|---|---|---|---|---|---|---|---|
| 0.5 | 500 | 8/8 | 15/15 | 1 | 0 | 7 (2 / 1 / 0 / 4) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `82FDF881` |
| 1.5 | 500 | 8/8 | 15/15 | 1 | 0 | 8 (1 / 1 / 3 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `62609F50` |
| 3 | 500 | 8/8 | 15/15 | 1 | 0 | 18 (1 / 1 / 13 / 3) | Grunt 2, Brute 6, Scout 0 | Coin 6, Potion 5, Key 3, Legendary 1 | `35B62C72` |

## 5. Bobot enemy (20 generation, seed 1..20)

| Bobot | Total enemy | Grunt (aktual) | Grunt (target) | Brute (aktual) | Brute (target) | Scout (aktual) | Scout (target) | Rata-rata ditolak |
|---|---|---|---|---|---|---|---|---|
| 60:30:10 | 160 | 92 (57.5%) | 60% | 49 (30.63%) | 30% | 19 (11.88%) | 10% | 8.55 |
| 33:33:34 | 160 | 48 (30%) | 33% | 53 (33.13%) | 33% | 59 (36.88%) | 34% | 8.55 |
