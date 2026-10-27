package com.manwell.photospike;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.media.*;
import android.os.SystemClock;
import org.json.JSONObject;
import java.io.File;

public final class NoteAudioBridge {
    private static MediaPlayer player;
    private static AudioFocusRequest focus;
    private static AudioManager manager;
    private static volatile boolean playing;
    private static volatile String playbackError = "";
    static SharedPreferences prefs(Context c) { return c.getSharedPreferences("note_recording", Context.MODE_PRIVATE); }
    static boolean busy(String phase) { return phase.equals("starting") || phase.equals("recording") || phase.equals("stopping"); }

    static File file(Context c, String root, String relative) throws Exception {
        File base = new File(root).getCanonicalFile();
        File external = c.getExternalFilesDir(null);
        if (!base.equals(c.getFilesDir().getCanonicalFile()) &&
                (external == null || !base.equals(external.getCanonicalFile())))
            throw new IllegalArgumentException("Invalid recording folder.");
        if (!relative.matches("audio/[a-f0-9]{32}/[0-9]{4}_[0-9]{2}_[0-9]{2}_[0-9]{2}_[0-9]{2}\\.m4a"))
            throw new IllegalArgumentException("Invalid recording filename.");
        File result = new File(base, relative).getCanonicalFile();
        if (!result.getPath().startsWith(base.getPath() + File.separator)) throw new IllegalArgumentException("Invalid path.");
        return result;
    }

    public static void begin(Activity activity, String root, String noteId, String id, String relative, int minutes) {
        activity.runOnUiThread(() -> {
            SharedPreferences p = prefs(activity);
            if (!p.getString("phase", "").isEmpty() || NoteRecordingService.instance != null) return;
            try {
                stopPlayer();
                if (!noteId.matches("[a-f0-9]{32}") || !id.matches("[a-f0-9]{32}") || !relative.startsWith("audio/" + noteId + "/"))
                    throw new IllegalArgumentException("Invalid recording identity.");
                File output = file(activity, root, relative);
                if (output.exists()) throw new IllegalStateException("This recording file already exists.");
                if (!output.getParentFile().isDirectory() && !output.getParentFile().mkdirs()) throw new IllegalStateException("Cannot create recording folder.");
                if (!p.edit().clear().putString("id", id).putString("noteId", noteId).putString("root", root)
                    .putString("path", relative).putString("phase", "starting")
                    .putLong("requested", System.currentTimeMillis()).putInt("minutes", Math.max(1, Math.min(720, minutes))).commit())
                    throw new IllegalStateException("Cannot save recording request.");
                activity.startForegroundService(new Intent(activity, NoteRecordingService.class).setAction("START"));
            } catch (Exception e) {
                p.edit().putString("id", id).putString("noteId", noteId).putString("phase", "error")
                    .putString("message", "Recording could not start: " + e.getMessage()).commit();
            }
        });
    }

    public static void stop(Activity activity) {
        activity.runOnUiThread(() -> { if (NoteRecordingService.instance != null) NoteRecordingService.instance.finish(""); });
    }

    public static String state(Activity activity) {
        SharedPreferences p = prefs(activity);
        String phase = p.getString("phase", "");
        if (NoteRecordingService.instance == null &&
            (phase.equals("recording") || phase.equals("stopping") ||
             (phase.equals("starting") && System.currentTimeMillis() - p.getLong("requested", 0) > 30000))) {
            // Never restart microphone capture automatically after process death.
            long duration = 0;
            try { duration = duration(file(activity, p.getString("root", ""), p.getString("path", ""))); } catch (Exception ignored) { }
            p.edit().putString("phase", duration > 0 ? "ready" : "error").putLong("durationMs", duration)
                .putString("message", duration > 0 ? "Recovered the completed part of an interrupted recording." : "Recording was interrupted and could not be recovered. The note was kept.").commit();
            phase = p.getString("phase", "");
        }
        try {
            JSONObject result = new JSONObject();
            result.put("id", p.getString("id", ""));
            result.put("noteId", p.getString("noteId", ""));
            result.put("phase", phase);
            result.put("path", p.getString("path", ""));
            result.put("message", p.getString("message", ""));
            result.put("durationMs", phase.equals("recording") ? Math.max(0, SystemClock.elapsedRealtime() - p.getLong("started", 0)) : p.getLong("durationMs", 0));
            result.put("playing", playing);
            result.put("playbackError", playbackError);
            return result.toString();
        } catch (Exception e) { throw new IllegalStateException(e); }
    }

    static long duration(File file) {
        MediaMetadataRetriever metadata = new MediaMetadataRetriever();
        try {
            metadata.setDataSource(file.getPath());
            return Long.parseLong(metadata.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION));
        } catch (Exception e) { return 0; }
        finally { try { metadata.release(); } catch (Exception ignored) { } }
    }

    public static void acknowledge(Activity activity, String id) {
        activity.runOnUiThread(() -> {
            SharedPreferences p = prefs(activity);
            if (id.equals(p.getString("id", "")) && !busy(p.getString("phase", ""))) p.edit().clear().commit();
        });
    }

    public static void play(Activity activity, String root, String relative) {
        activity.runOnUiThread(() -> {
            stopPlayer();
            playbackError = "";
            if (busy(prefs(activity).getString("phase", ""))) { playbackError = "Stop recording before playback."; return; }
            try {
                File file = file(activity, root, relative);
                manager = (AudioManager) activity.getSystemService(Context.AUDIO_SERVICE);
                AudioAttributes attributes = new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_MEDIA)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH).build();
                focus = new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT).setAudioAttributes(attributes)
                    .setOnAudioFocusChangeListener(change -> { if (change < 0) stopPlayer(); }).build();
                if (manager.requestAudioFocus(focus) != AudioManager.AUDIOFOCUS_REQUEST_GRANTED)
                    throw new IllegalStateException("Audio playback is currently unavailable.");
                player = new MediaPlayer();
                player.setAudioAttributes(attributes);
                player.setDataSource(file.getPath());
                player.setOnPreparedListener(p -> { if (p == player) { p.start(); playing = true; } });
                player.setOnCompletionListener(p -> stopPlayer());
                player.setOnErrorListener((p, what, extra) -> { playbackError = "This recording could not be played."; stopPlayer(); return true; });
                player.prepareAsync();
            } catch (Exception e) { playbackError = "Playback failed: " + e.getMessage(); stopPlayer(); }
        });
    }
    public static void stopPlayback(Activity activity) { activity.runOnUiThread(NoteAudioBridge::stopPlayer); }
    private static void stopPlayer() {
        if (player != null) { player.release(); player = null; }
        playing = false;
        if (manager != null && focus != null) manager.abandonAudioFocusRequest(focus);
        focus = null;
    }
}
