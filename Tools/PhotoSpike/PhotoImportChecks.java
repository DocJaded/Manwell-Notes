import java.io.*;
import java.lang.reflect.*;
import java.nio.file.*;
import java.util.Arrays;

/** Host-side checks for the import boundary helpers; does not pretend to emulate Android UI. */
public final class PhotoImportChecks {
    private static Method copy;
    private static Method animated;
    private static int checks;

    public static void main(String[] args) throws Exception {
        Class<?> activity = Class.forName("com.manwell.photospike.PhotoActivity");
        copy = activity.getDeclaredMethod("copyBounded", InputStream.class, File.class);
        animated = activity.getDeclaredMethod("isAnimatedPng", File.class);
        copy.setAccessible(true);
        animated.setAccessible(true);
        Path directory = Paths.get(args[0]);
        Files.createDirectories(directory);
        File copied = directory.resolve("copied.tmp").toFile();
        byte[] sample = new byte[]{1, 2, 3, 4, 5};
        copy.invoke(null, new ByteArrayInputStream(sample), copied);
        check(Arrays.equals(sample, Files.readAllBytes(copied.toPath())), "copy preserves bytes");
        expectIo(() -> copy.invoke(null, new ByteArrayInputStream(new byte[0]), copied), "empty source rejected");
        expectIo(() -> copy.invoke(null, null, copied), "missing source rejected");
        InputStream oversized = new InputStream() {
            private long remaining = 40L * 1024 * 1024 + 1;
            @Override public int read() { return remaining-- > 0 ? 0 : -1; }
            @Override public int read(byte[] buffer, int offset, int length) {
                if (remaining <= 0) return -1;
                int count = (int)Math.min(remaining, length);
                remaining -= count;
                return count;
            }
        };
        expectIo(() -> copy.invoke(null, oversized, copied), "oversized source rejected");
        File still = png(directory, "still.png", 0x49454e44, 0);
        check(!(boolean)animated.invoke(null, still), "still PNG permitted by animation filter");
        File apng = png(directory, "animated.png", 0x6163544c, 0);
        check((boolean)animated.invoke(null, apng), "APNG detected");
        File corrupt = png(directory, "corrupt.png", 0x49444154, 100);
        expectIo(() -> animated.invoke(null, corrupt), "invalid chunk length rejected");
        System.out.println("PASS: " + checks + " photo import boundary checks.");
    }

    private static File png(Path directory, String name, int type, int length) throws IOException {
        File file = directory.resolve(name).toFile();
        try (DataOutputStream stream = new DataOutputStream(new FileOutputStream(file))) {
            stream.writeLong(0x89504e470d0a1a0aL);
            stream.writeInt(length);
            stream.writeInt(type);
            stream.writeInt(0);
        }
        return file;
    }
    private interface Throwing { void run() throws Exception; }
    private static void expectIo(Throwing operation, String name) throws Exception {
        try { operation.run(); }
        catch (InvocationTargetException exception) {
            check(exception.getCause() instanceof IOException, name);
            return;
        }
        throw new AssertionError(name);
    }
    private static void check(boolean condition, String name) {
        if (!condition) throw new AssertionError(name);
        checks++;
    }
}
