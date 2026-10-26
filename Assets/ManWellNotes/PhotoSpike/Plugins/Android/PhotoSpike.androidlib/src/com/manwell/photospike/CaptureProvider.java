package com.manwell.photospike;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileNotFoundException;

/** Exposes only the active capture's single temporary file, via an explicit URI grant. */
public final class CaptureProvider extends ContentProvider {
    @Override public boolean onCreate() { return true; }

    private File resolve(Uri uri) throws FileNotFoundException {
        String active = PhotoBridge.prefs(getContext()).getString("capture", "");
        if (!"content".equals(uri.getScheme()) ||
                !(getContext().getPackageName() + ".photospike.capture").equals(uri.getAuthority()) ||
                !active.matches("[a-f0-9-]+\\.jpg") ||
                !("/" + active).equals(uri.getPath()))
            throw new FileNotFoundException("No active capture at this URI.");
        return new File(PhotoBridge.directory(getContext()), active);
    }

    @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        File file = resolve(uri);
        if (!mode.equals("r") && !mode.equals("w") && !mode.equals("wt") && !mode.equals("rw") && !mode.equals("rwt"))
            throw new FileNotFoundException("Unsupported mode.");
        return ParcelFileDescriptor.open(file, ParcelFileDescriptor.parseMode(mode));
    }

    @Override public String getType(Uri uri) { return "image/jpeg"; }
    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sort) {
        try {
            File file = resolve(uri);
            if (projection == null) projection = new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE};
            MatrixCursor cursor = new MatrixCursor(projection, 1);
            Object[] row = new Object[projection.length];
            for (int i = 0; i < projection.length; i++) {
                if (OpenableColumns.DISPLAY_NAME.equals(projection[i])) row[i] = file.getName();
                else if (OpenableColumns.SIZE.equals(projection[i])) row[i] = file.length();
            }
            cursor.addRow(row);
            return cursor;
        } catch (FileNotFoundException exception) { return null; }
    }
    @Override public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException(); }
    @Override public int update(Uri uri, ContentValues values, String where, String[] args) { throw new UnsupportedOperationException(); }
    @Override public int delete(Uri uri, String where, String[] args) { throw new UnsupportedOperationException(); }
}
