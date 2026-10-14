package mtgb.bridge;

import forge.gui.interfaces.IGuiBase;
import forge.game.player.PlayerController;
import java.lang.reflect.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.atomic.AtomicBoolean;

/** Adapter/inheritance contract only; these are not card effect scenarios. */
public final class BaseCallbackFixture {
    static void check(boolean b,String message){if(!b)throw new AssertionError(message);}
    static void unsupported(Runnable call,String method) {
        try {call.run();throw new AssertionError("Unsupported callback silently accepted: "+method);}
        catch(UnsupportedOperationException e){check(e.getMessage().contains("IGuiBase."+method),"Wrong unsupported boundary");}
    }
    static void resolve(Path output)throws Exception {
        List<Object> rows=new ArrayList<>();
        for(Method declaration:PlayerController.class.getDeclaredMethods()) {
            int mods=declaration.getModifiers();if(Modifier.isStatic(mods)||Modifier.isPrivate(mods)||declaration.isSynthetic())continue;
            Method resolved=null;
            for(Class<?> owner=ForgeBridge.BridgeController.class;owner!=null;owner=owner.getSuperclass()) {
                try {resolved=owner.getDeclaredMethod(declaration.getName(),declaration.getParameterTypes());break;}
                catch(NoSuchMethodException ignored){}
            }
            check(resolved!=null&&!Modifier.isAbstract(resolved.getModifiers()),"Unresolved controller: "+declaration);
            rows.add(Map.of("signature",declaration.toGenericString(),"implementation",resolved.toGenericString(),
                "declaringClass",resolved.getDeclaringClass().getName(),"nativeAbstract",Modifier.isAbstract(mods)));
        }
        rows.sort(Comparator.comparing(v->((Map<?,?>)v).get("signature").toString()));
        Files.writeString(output,ForgeBridge.JSON.toJson(Map.of("status","PASS","scope","PlayerController declared nonprivate instance methods; reflection resolution only","methods",rows)));
    }
    public static void main(String[] args)throws Exception {
        IGuiBase base=(IGuiBase)Proxy.newProxyInstance(IGuiBase.class.getClassLoader(),new Class<?>[]{IGuiBase.class},
            (p,m,a)->ForgeBridge.base(p,m,a,"pinned-assets"));
        if(args[0].equals("base-unsupported")) {
            AtomicBoolean ran=new AtomicBoolean();unsupported(()->base.runBackgroundTask("decision",()->ran.set(true)),"runBackgroundTask");
            check(!ran.get(),"Unsupported background task executed");
            unsupported(()->base.showBoxedProduct("choice","message",List.of()),"showBoxedProduct");
            unsupported(()->base.getChoices("choice",0,1,List.of("a"),List.of(),null),"getChoices");
            unsupported(()->base.copyToClipboard("data"),"copyToClipboard");
        } else if(args[0].equals("base-defaults")) {
            check(base.isRunningOnDesktop()&&!base.isLibgdxPort()&&!base.hasNetGame(),"Native capability mismatch");
            check(base.getAssetsDir().equals("pinned-assets")&&base.getScreenScale()==1f,"Environment mismatch");
            base.vibrate(1,1);base.vibrateController(1,.5f);check(!base.useControllerForHaptics(),"Default haptics changed");
            base.clearImageCache();base.preventSystemSleep(false);base.startAltSoundSystem("none",false);
            unsupported(()->base.getCardArt(null),"getCardArt");
        } else throw new IllegalArgumentException(args[0]);
        resolve(Path.of(args[1]));
        System.out.println("PASS "+args[0]+": explicit native capabilities/default forwarding and unsupported decision rejection; controller inheritance resolution is structural only");
    }
}
