using UnityEditor;
using UnityEngine;

// Only the game-selected copies are configured; purchased source assets stay intact.
public class NovelAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
        AudioImporter importer = (AudioImporter)assetImporter;
        bool music = assetPath.EndsWith("Campus.wav") || assetPath.EndsWith("Study.wav") || assetPath.EndsWith("Crisis.wav");
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = music ? UnityEngine.AudioClipLoadType.Streaming : UnityEngine.AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = .7f;
        settings.preloadAudioData = !music;
        importer.defaultSampleSettings = settings;
    }
}
