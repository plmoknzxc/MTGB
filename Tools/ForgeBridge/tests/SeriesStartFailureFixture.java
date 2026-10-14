package mtgb.bridge;

/** Explicit worker-start failure injection; no card state or rule result is fabricated. */
public final class SeriesStartFailureFixture {
    public static void main(String[] args) { System.exit(7); }
}
