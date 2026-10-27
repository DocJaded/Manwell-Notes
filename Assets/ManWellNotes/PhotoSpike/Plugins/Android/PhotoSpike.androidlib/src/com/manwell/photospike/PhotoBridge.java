package com.manwell.photospike;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import org.json.JSONObject;
import java.io.File;
import java.util.UUID;

public final class PhotoBridge {
    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences("manwell_photo_spike", Context.MODE_PRIVATE);
    }
    static File directory(Context context) {
        File directory = new File(context.getFilesDir(), "photo-spike");
        if (!directory.isDirectory() && !directory.mkdirs())
            throw new IllegalStateException("Not enough storage to prepare a photo.");
        return directory;
    }

    public static void begin(Activity activity, String source, int size) {
        beginRequest(activity, source, size, UUID.randomUUID().toString());
    }

    // The notes app persists this ID together with the initiating note before calling us.
    public static synchronized boolean beginRequest(Activity activity, String source, int size, String id) {
        if (!source.equals("camera") && !source.equals("picker")) throw new IllegalArgumentException("Images only.");
        if (id == null || !id.matches("[a-f0-9-]{32,36}")) throw new IllegalArgumentException("Invalid request ID.");
        if (prefs(activity).getBoolean("pending", false) || PhotoActivity.isImporting()) return false;
        if (!prefs(activity).edit().putString("id", id).putString("source", source)
                .putInt("size", Math.max(256, Math.min(size, 2048)))
                .putString("capture", id + ".jpg").putBoolean("pending", true)
                .putString("phase", "launch").putString("outcome", "pending").commit()) return false;
        activity.runOnUiThread(() -> {
            try {
                activity.startActivity(new Intent(activity, PhotoActivity.class).putExtra("requestId", id));
            } catch (Exception exception) { finish(activity, "error", "Could not open photo source: " + exception.getMessage()); }
        });
        return true;
    }

    public static String state(Activity activity) {
        SharedPreferences p = prefs(activity);
        try {
            JSONObject result = new JSONObject();
            result.put("id", p.getString("id", ""));
            result.put("pending", p.getBoolean("pending", false) || PhotoActivity.isImporting());
            result.put("outcome", p.getString("outcome", "ready"));
            result.put("message", p.getString("message", "Take a photo or choose a stored image. This test keeps only the latest accepted image."));
            result.put("path", p.getString("path", ""));
            result.put("route", p.getString("savedRoute", ""));
            result.put("width", p.getInt("width", 0));
            result.put("height", p.getInt("height", 0));
            return result.toString();
        } catch (Exception exception) { throw new IllegalStateException(exception); }
    }

    public static void clear(Activity activity) {
        activity.runOnUiThread(() -> {
            SharedPreferences p = prefs(activity);
            if (p.getBoolean("pending", false)) return;
            String old = p.getString("path", "");
            if (!p.edit().remove("path").remove("width").remove("height").remove("savedRoute")
                    .putString("message", "Test image cleared.").putString("outcome", "ready").commit()) {
                finish(activity, "error", "Could not clear the saved image.");
                return;
            }
            deleteOwned(activity, old);
        });
    }

    public static void acknowledge(Activity activity, String id) {
        activity.runOnUiThread(() -> {
            SharedPreferences p = prefs(activity);
            if (!id.equals(p.getString("id", "")) || p.getBoolean("pending", false) || PhotoActivity.isImporting()) return;
            String old = p.getString("path", "");
            if (p.edit().remove("path").remove("width").remove("height").putString("outcome", "ready").commit())
                deleteOwned(activity, old);
        });
    }

    public static void resetInterruptedRequest(Activity activity) {
        activity.runOnUiThread(() -> {
            if (PhotoActivity.isImporting()) return;
            finish(activity, "cancelled", "Interrupted request reset. The saved image is unchanged.");
        });
    }

    static void finish(Context context, String outcome, String message) {
        prefs(context).edit().putBoolean("pending", false).putString("phase", "done")
                .putString("outcome", outcome).putString("message", message).commit();
        String capture = prefs(context).getString("capture", "");
        if (capture.matches("[a-f0-9-]+\\.jpg")) deleteOwned(context, new File(directory(context), capture).getPath());
        prefs(context).edit().remove("capture").apply();
    }

    static void deleteOwned(Context context, String path) {
        if (path.isEmpty()) return;
        try {
            File file = new File(path).getCanonicalFile();
            if (directory(context).getCanonicalFile().equals(file.getParentFile())) file.delete();
        } catch (Exception ignored) { /* A leftover test file is safer than deleting outside this directory. */ }
    }
}
