package com.manwell.photospike;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.media.MediaRecorder;
import android.os.*;
import java.io.File;

public final class NoteRecordingService extends Service {
    static volatile NoteRecordingService instance;
    private MediaRecorder recorder;
    private PowerManager.WakeLock wakeLock;
    private File output;
    private boolean finishing;
    private static final String CHANNEL = "quiet_note_recording_v1";

    @Override public void onCreate() { super.onCreate(); instance = this; }
    @Override public IBinder onBind(Intent intent) { return null; }
    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && "STOP".equals(intent.getAction())) {
            if (recorder != null) finish(""); else stopSelf();
            return START_NOT_STICKY;
        }
        if (recorder != null || finishing) return START_NOT_STICKY;
        SharedPreferences p = NoteAudioBridge.prefs(this);
        if (!"starting".equals(p.getString("phase", ""))) { stopSelf(); return START_NOT_STICKY; }
        try {
            NotificationManager notifications = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
            NotificationChannel channel = new NotificationChannel(CHANNEL, "Sound recording", NotificationManager.IMPORTANCE_LOW);
            channel.setSound(null, null);
            channel.enableVibration(false);
            notifications.createNotificationChannel(channel);
            Intent open = getPackageManager().getLaunchIntentForPackage(getPackageName());
            PendingIntent show = PendingIntent.getActivity(this, 701, open, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            PendingIntent stop = PendingIntent.getService(this, 702, new Intent(this, NoteRecordingService.class).setAction("STOP"), PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            Notification.Builder builder = new Notification.Builder(this, CHANNEL)
                .setSmallIcon(android.R.drawable.ic_btn_speak_now).setContentTitle("Recording sound")
                .setContentText("Tap Stop to save your recording.").setContentIntent(show)
                .setOngoing(true).setOnlyAlertOnce(true).setDefaults(0).setSound(null).setVisibility(Notification.VISIBILITY_PUBLIC)
                .setWhen(System.currentTimeMillis()).setUsesChronometer(true)
                .addAction(new Notification.Action.Builder(null, "Stop", stop).build());
            if (Build.VERSION.SDK_INT >= 30) startForeground(701, builder.build(), ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE);
            else startForeground(701, builder.build());
            output = NoteAudioBridge.file(this, p.getString("root", ""), p.getString("path", ""));
            recorder = Build.VERSION.SDK_INT >= 31 ? new MediaRecorder(this) : new MediaRecorder();
            recorder.setAudioSource(MediaRecorder.AudioSource.MIC);
            recorder.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4);
            recorder.setAudioEncoder(MediaRecorder.AudioEncoder.AAC);
            recorder.setAudioChannels(1);
            recorder.setAudioSamplingRate(44100);
            recorder.setAudioEncodingBitRate(96000);
            recorder.setOutputFile(output.getPath());
            int minutes = p.getInt("minutes", 120);
            recorder.setMaxDuration(minutes * 60000);
            recorder.setMaxFileSize(256L * 1024 * 1024);
            recorder.setOnInfoListener((r, what, extra) -> {
                if (what == MediaRecorder.MEDIA_RECORDER_INFO_MAX_DURATION_REACHED || what == MediaRecorder.MEDIA_RECORDER_INFO_MAX_FILESIZE_REACHED)
                    finish("Recording saved at the configured duration or size limit.");
            });
            recorder.setOnErrorListener((r, what, extra) -> finish("Recording stopped because the microphone became unavailable."));
            recorder.prepare();
            recorder.start();
            wakeLock = ((PowerManager) getSystemService(POWER_SERVICE)).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, getPackageName() + ":noteRecording");
            wakeLock.acquire(minutes * 60000L + 60000L);
            if (!p.edit().putString("phase", "recording").putLong("started", SystemClock.elapsedRealtime()).commit())
                throw new IllegalStateException("Cannot save recording state.");
        } catch (Exception e) { finish("Recording could not start: " + e.getMessage()); }
        return START_NOT_STICKY;
    }

    void finish(String message) {
        if (finishing) return;
        finishing = true;
        SharedPreferences p = NoteAudioBridge.prefs(this);
        p.edit().putString("phase", "stopping").commit();
        if (recorder != null) {
            try { recorder.stop(); } catch (RuntimeException ignored) { }
            recorder.release();
            recorder = null;
        }
        long duration = output == null ? 0 : NoteAudioBridge.duration(output);
        if (duration <= 0 && message.isEmpty()) message = "No playable sound was saved. Try recording for a little longer.";
        p.edit().putString("phase", duration > 0 ? "ready" : "error").putLong("durationMs", duration)
            .putString("message", message).commit();
        if (duration <= 0 && output != null) output.delete();
        if (wakeLock != null && wakeLock.isHeld()) wakeLock.release();
        stopForeground(STOP_FOREGROUND_REMOVE);
        stopSelf();
    }
    @Override public void onDestroy() {
        if (!finishing && recorder != null) finish("Recording was interrupted; the completed audio was saved where possible.");
        if (wakeLock != null && wakeLock.isHeld()) wakeLock.release();
        instance = null;
        super.onDestroy();
    }
}
