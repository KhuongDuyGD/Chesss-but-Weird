package com.chessbutweird.share;

import android.app.Activity;
import android.content.ClipData;
import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.ClipboardManager;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.util.UUID;

/** Read-only, grant-scoped PNG provider. No storage permissions or AndroidX dependency. */
public final class ResultImageProvider extends ContentProvider {
    private static final String DIRECTORY = "cbw-result-share";

    public static void copy(Activity activity, String source) throws IOException {
        File directory = new File(activity.getCacheDir(), DIRECTORY);
        if (!directory.isDirectory() && !directory.mkdirs()) throw new IOException("Could not prepare the clipboard image.");
        File[] oldFiles = directory.listFiles();
        if (oldFiles != null) for (File file : oldFiles)
            if (file.lastModified() < System.currentTimeMillis() - 7L * 24 * 60 * 60 * 1000) file.delete();
        File image = new File(directory, "cbw-" + UUID.randomUUID() + ".png");
        try (FileInputStream input = new FileInputStream(source); FileOutputStream output = new FileOutputStream(image)) {
            byte[] buffer = new byte[16384]; int count;
            while ((count = input.read(buffer)) != -1) output.write(buffer, 0, count);
        }
        Uri uri = new Uri.Builder().scheme("content").authority(activity.getPackageName() + ".cbw.resultshare")
            .appendPath(image.getName()).build();
        ClipboardManager clipboard = (ClipboardManager)activity.getSystemService(Context.CLIPBOARD_SERVICE);
        if (clipboard == null) throw new IOException("The image clipboard is unavailable.");
        clipboard.setPrimaryClip(ClipData.newUri(activity.getContentResolver(), "Chess but Weird result", uri));
    }

    private File image(Uri uri) throws FileNotFoundException {
        String name = uri.getLastPathSegment();
        if (uri.getPathSegments().size() != 1 || name == null || !name.matches("cbw-[a-fA-F0-9-]+\\.png"))
            throw new FileNotFoundException("Invalid result image.");
        File file = new File(new File(getContext().getCacheDir(), DIRECTORY), name);
        if (!file.isFile()) throw new FileNotFoundException("Result image no longer exists.");
        return file;
    }
    @Override public boolean onCreate() { return true; }
    @Override public String getType(Uri uri) { return "image/png"; }
    @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        if (!"r".equals(mode)) throw new FileNotFoundException("Result images are read-only.");
        return ParcelFileDescriptor.open(image(uri), ParcelFileDescriptor.MODE_READ_ONLY);
    }
    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sort) {
        try {
            File file = image(uri);
            String[] columns = projection == null ? new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE} : projection;
            MatrixCursor cursor = new MatrixCursor(columns, 1);
            Object[] values = new Object[columns.length];
            for (int i = 0; i < columns.length; i++)
                values[i] = OpenableColumns.DISPLAY_NAME.equals(columns[i]) ? file.getName() :
                    OpenableColumns.SIZE.equals(columns[i]) ? file.length() : null;
            cursor.addRow(values); return cursor;
        } catch (FileNotFoundException error) { return null; }
    }
    @Override public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException("Read-only"); }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { return 0; }
    @Override public int delete(Uri uri, String selection, String[] args) { return 0; }
}
