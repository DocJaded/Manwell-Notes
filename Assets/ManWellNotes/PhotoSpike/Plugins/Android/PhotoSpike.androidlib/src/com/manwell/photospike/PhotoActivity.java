package com.manwell.photospike;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.provider.MediaStore;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.concurrent.atomic.AtomicBoolean;

/** Temporary activity owns Android results, including activity/process recreation. No Unity dependency. */
public final class PhotoActivity extends Activity {
    private static final int REQUEST_IMAGE = 4102;
    private static final long MAX_SOURCE_BYTES = 40L * 1024 * 1024;
    private static final long MAX_SOURCE_PIXELS = 100_000_000L;
    private static final AtomicBoolean importing = new AtomicBoolean();
    static boolean isImporting() { return importing.get(); }
    private String requestId;
    private final Handler handler = new Handler(android.os.Looper.getMainLooper());
    private final Runnable completion = new Runnable() {
        @Override public void run() {
            if (!isCurrentRequest()) finish();
            else handler.postDelayed(this, 300);
        }
    };

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        requestId = getIntent().getStringExtra("requestId");
        SharedPreferences p = PhotoBridge.prefs(this);
        if (!isCurrentRequest()) { finish(); return; }
        handler.post(completion);
        if ("importing".equals(p.getString("phase", ""))) { importImage(); return; }
        if (state != null) return; // Android will deliver the outstanding camera/picker result.
        try {
            Intent intent;
            if ("camera".equals(p.getString("source", ""))) {
                File output = new File(PhotoBridge.directory(this), p.getString("capture", ""));
                if (!output.createNewFile()) throw new IOException("Could not prepare camera output.");
                Uri uri = Uri.parse("content://" + getPackageName() + ".photospike.capture/" + output.getName());
                intent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
                intent.putExtra(MediaStore.EXTRA_OUTPUT, uri);
                intent.setClipData(ClipData.newRawUri("Photo output", uri));
                intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_READ_URI_PERMISSION);
                p.edit().putString("route", "Device camera").commit();
            } else {
                intent = new Intent(MediaStore.ACTION_PICK_IMAGES).setType("image/*");
                if (Build.VERSION.SDK_INT < 33 || intent.resolveActivity(getPackageManager()) == null) {
                    intent = new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("image/*")
                            .addCategory(Intent.CATEGORY_OPENABLE);
                    p.edit().putString("route", "Document picker fallback").commit();
                } else p.edit().putString("route", "System Photo Picker").commit();
            }
            p.edit().putString("phase", "awaiting").commit();
            startActivityForResult(intent, REQUEST_IMAGE);
        } catch (Exception exception) { fail("Could not open photo source: " + exception.getMessage()); }
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST_IMAGE) return;
        SharedPreferences p = PhotoBridge.prefs(this);
        if (!isCurrentRequest()) { finish(); return; }
        if (result != RESULT_OK) {
            PhotoBridge.finish(this, "cancelled", "Cancelled. The previous test image is unchanged.");
            finish();
            return;
        }
        if ("picker".equals(p.getString("source", ""))) {
            Uri uri = data != null ? data.getData() : null;
            if (uri == null || !"content".equals(uri.getScheme())) { fail("No readable image was selected."); return; }
            try { getContentResolver().takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION); }
            catch (SecurityException ignored) { /* Transient access is sufficient for immediate copying. */ }
            p.edit().putString("uri", uri.toString()).commit();
        }
        p.edit().putString("phase", "importing").commit();
        importImage();
    }

    private void importImage() {
        if (!importing.compareAndSet(false, true)) return;
        new Thread(() -> {
            SharedPreferences p = PhotoBridge.prefs(this);
            String id = p.getString("id", "");
            File input = new File(PhotoBridge.directory(this), id + "-input.tmp");
            File output = new File(PhotoBridge.directory(this), id + "-saved.png");
            Bitmap bitmap = null;
            boolean saved = false;
            try {
                if ("camera".equals(p.getString("source", ""))) {
                    File capture = new File(PhotoBridge.directory(this), p.getString("capture", ""));
                    try (InputStream stream = new FileInputStream(capture)) { copyBounded(stream, input); }
                } else {
                    Uri uri = Uri.parse(p.getString("uri", ""));
                    String mime = getContentResolver().getType(uri);
                    if (mime != null && !mime.startsWith("image/")) throw new IOException("Only still images are supported.");
                    try (InputStream stream = getContentResolver().openInputStream(uri)) { copyBounded(stream, input); }
                }
                BitmapFactory.Options bounds = new BitmapFactory.Options();
                bounds.inJustDecodeBounds = true;
                BitmapFactory.decodeFile(input.getPath(), bounds);
                // The spike deliberately accepts JPEG/PNG only; unsupported formats are explicit.
                if (!"image/jpeg".equals(bounds.outMimeType) && !"image/png".equals(bounds.outMimeType))
                    throw new IOException("This first test supports JPEG and PNG still images only.");
                if ("image/png".equals(bounds.outMimeType) && isAnimatedPng(input))
                    throw new IOException("Animated images are not supported.");
                if (bounds.outWidth <= 0 || bounds.outHeight <= 0 ||
                        (long)bounds.outWidth * bounds.outHeight > MAX_SOURCE_PIXELS)
                    throw new IOException("The selected image is invalid or larger than 100 megapixels.");
                int limit = p.getInt("size", 2048);
                BitmapFactory.Options options = new BitmapFactory.Options();
                options.inSampleSize = 1;
                while (Math.max(bounds.outWidth, bounds.outHeight) / options.inSampleSize > limit)
                    options.inSampleSize *= 2;
                bitmap = BitmapFactory.decodeFile(input.getPath(), options);
                if (bitmap == null) throw new IOException("The selected image could not be decoded.");
                ExifInterface exif = new ExifInterface(input.getPath());
                int orientation = exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL);
                Matrix matrix = new Matrix();
                switch (orientation) {
                    case 2: matrix.setScale(-1, 1); break;
                    case 3: matrix.setRotate(180); break;
                    case 4: matrix.setScale(1, -1); break;
                    case 5: matrix.setRotate(90); matrix.postScale(-1, 1); break;
                    case 6: matrix.setRotate(90); break;
                    case 7: matrix.setRotate(-90); matrix.postScale(-1, 1); break;
                    case 8: matrix.setRotate(-90); break;
                }
                Bitmap oriented = Bitmap.createBitmap(bitmap, 0, 0, bitmap.getWidth(), bitmap.getHeight(), matrix, true);
                if (oriented != bitmap) { bitmap.recycle(); bitmap = oriented; }
                try (FileOutputStream stream = new FileOutputStream(output)) {
                    if (!bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream)) throw new IOException("Could not encode image.");
                    stream.getFD().sync();
                }
                String previous = p.getString("path", "");
                // The durable pointer is updated only after the new image has been completely written.
                if (!p.edit().putString("path", output.getPath()).putInt("width", bitmap.getWidth())
                        .putInt("height", bitmap.getHeight()).putString("savedRoute", p.getString("route", ""))
                        .putBoolean("pending", false).putString("phase", "done")
                        .putString("outcome", "success").putString("message", "Photo saved privately in this test app.").commit())
                    throw new IOException("Could not save the image record.");
                saved = true;
                PhotoBridge.deleteOwned(this, previous);
                PhotoBridge.finish(this, "success", "Photo saved privately in this test app.");
            } catch (Exception | OutOfMemoryError exception) {
                PhotoBridge.finish(this, "error", "Image was not added: " + exception.getMessage());
            } finally {
                if (bitmap != null) bitmap.recycle();
                PhotoBridge.deleteOwned(this, input.getPath());
                if (!saved) PhotoBridge.deleteOwned(this, output.getPath());
                String uri = p.getString("uri", "");
                if (!uri.isEmpty()) {
                    try { getContentResolver().releasePersistableUriPermission(Uri.parse(uri), Intent.FLAG_GRANT_READ_URI_PERMISSION); }
                    catch (Exception ignored) { }
                    p.edit().remove("uri").apply();
                }
                importing.set(false);
                runOnUiThread(this::finish);
            }
        }, "PhotoSpikeImport").start();
    }

    private static void copyBounded(InputStream input, File target) throws IOException {
        if (input == null) throw new IOException("The image provider returned no data.");
        try (FileOutputStream output = new FileOutputStream(target)) {
            byte[] buffer = new byte[32768];
            long total = 0;
            int count;
            while ((count = input.read(buffer)) != -1) {
                total += count;
                if (total > MAX_SOURCE_BYTES) throw new IOException("Choose an image smaller than 40 MB.");
                output.write(buffer, 0, count);
            }
            if (total == 0) throw new IOException("The image is empty.");
        }
    }

    private static boolean isAnimatedPng(File file) throws IOException {
        try (java.io.DataInputStream input = new java.io.DataInputStream(new FileInputStream(file))) {
            input.readLong(); // PNG signature was already checked by the Android decoder.
            long remaining = file.length() - 8;
            while (remaining >= 12) {
                long length = Integer.toUnsignedLong(input.readInt());
                int type = input.readInt();
                if (length > remaining - 12) throw new IOException("Invalid PNG chunk.");
                if (type == 0x6163544c) return true; // acTL declares APNG animation.
                if (type == 0x49454e44) return false; // IEND
                long skip = length + 4;
                while (skip > 0) {
                    int chunk = input.skipBytes((int)Math.min(skip, 32768));
                    if (chunk == 0) throw new IOException("Truncated PNG.");
                    skip -= chunk;
                }
                remaining -= length + 12;
            }
            return false;
        }
    }

    private void fail(String message) { PhotoBridge.finish(this, "error", message); finish(); }
    private boolean isCurrentRequest() {
        SharedPreferences p = PhotoBridge.prefs(this);
        return requestId != null && requestId.equals(p.getString("id", "")) && p.getBoolean("pending", false);
    }
    @Override protected void onDestroy() { handler.removeCallbacks(completion); super.onDestroy(); }
}
