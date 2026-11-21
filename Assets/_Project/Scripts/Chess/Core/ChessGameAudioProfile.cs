using UnityEngine;

[CreateAssetMenu(fileName = "ChessGameAudioProfile", menuName = "Chess/Audio Profile")]
public class ChessGameAudioProfile : ScriptableObject
{
    public AudioClip pickSound;
    public AudioClip moveSound;
    public AudioClip hitSound;
    public AudioClip errorSound;
    public AudioClip castleSound;
    public AudioClip promotionSound;
    public AudioClip checkSound;
    public AudioClip winSound;
}
