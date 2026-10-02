package mtgb.bridge;
import java.io.*;
import java.nio.charset.StandardCharsets;

/** Deliberately splits multibyte characters across pipe writes. No platform defaults. */
public final class Utf8PipeFixture {
    public static void main(String[] args)throws Exception {
        String input=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8)).readLine();
        if(input==null || input.startsWith("\uFEFF"))throw new IllegalStateException("Missing input or unexpected BOM");
        byte[] bytes=(input+"\n").getBytes(StandardCharsets.UTF_8);
        for(FileDescriptor fd:new FileDescriptor[]{FileDescriptor.out,FileDescriptor.err}) {
            OutputStream stream=new FileOutputStream(fd);
            for(int i=0;i<bytes.length;i+=7){stream.write(bytes,i,Math.min(7,bytes.length-i));stream.flush();}
        }
    }
}
