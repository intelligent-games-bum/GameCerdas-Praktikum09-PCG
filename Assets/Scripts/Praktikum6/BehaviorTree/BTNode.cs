using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Hasil satu kali evaluasi node Behavior Tree.</summary>
public enum NodeState
{
    /// <summary>Selesai dan berhasil.</summary>
    Success,
    /// <summary>Gagal / kondisi tidak terpenuhi. Parent boleh mencoba cabang lain.</summary>
    Failure,
    /// <summary>Masih dikerjakan, akan dievaluasi lagi tick berikutnya.</summary>
    Running,
}

/// <summary>
/// Dasar semua node Behavior Tree. Tree dievaluasi dari root setiap tick (setiap frame),
/// sehingga perubahan kondisi (HP turun, Player muncul) langsung mengubah cabang yang dipilih.
///
/// Setiap node mencatat hasil dan frame evaluasi terakhirnya supaya debugger bisa
/// menggambar node mana yang benar-benar dikunjungi pada frame ini.
/// </summary>
public abstract class BTNode
{
    protected BTNode(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public NodeState LastState { get; private set; } = NodeState.Failure;
    public int LastEvaluatedFrame { get; private set; } = -1;

    /// <summary>True jika node ini dievaluasi pada frame sekarang.</summary>
    public bool EvaluatedThisFrame => LastEvaluatedFrame == Time.frameCount;

    /// <summary>Anak node (kosong untuk leaf). Dipakai debugger untuk menggambar tree.</summary>
    public virtual IReadOnlyList<BTNode> Children => Array.Empty<BTNode>();

    /// <summary>Info tambahan untuk debugger, misal sisa cooldown.</summary>
    public virtual string Info => string.Empty;

    public NodeState Evaluate()
    {
        LastState = OnEvaluate();
        LastEvaluatedFrame = Time.frameCount;
        return LastState;
    }

    protected abstract NodeState OnEvaluate();
}
