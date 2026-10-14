// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.gui.GuiBase;
import forge.gui.interfaces.IGuiBase;
import forge.localinstance.properties.ForgePreferences.FPref;
import forge.model.FModel;
import java.io.File;
import java.lang.reflect.*;
import java.util.concurrent.*;

/** Headless presentation services. Rules continue to run on Forge's game thread. */
public final class ForgeEnvironment {
    public static final ExecutorService UI = Executors.newSingleThreadExecutor(r -> new Thread(r, "mtgb-forge-ui"));

    public static void initialize(String directory) throws Exception {
        initialize(directory, false);
    }

    static void initialize(String directory, boolean lazyCards) throws Exception {
        String assets = new File(directory).getCanonicalPath().replace('\\', '/') + "/";
        GuiBase.setInterface((IGuiBase) Proxy.newProxyInstance(IGuiBase.class.getClassLoader(),
            new Class<?>[]{IGuiBase.class}, (proxy, method, args) -> {
                String name = method.getName();
                switch (name) {
                    case "getAssetsDir": return assets;
                    case "getCurrentVersion": return "MTGB-Forge";
                    case "isRunningOnDesktop": return true;
                    case "isGuiThread": return Thread.currentThread().getName().equals("mtgb-forge-ui");
                    case "invokeInEdtLater": UI.execute((Runnable) args[0]); return null;
                    case "invokeInEdtNow": case "invokeInEdtAndWait":
                        if (Thread.currentThread().getName().equals("mtgb-forge-ui")) ((Runnable) args[0]).run();
                        else UI.submit((Runnable) args[0]).get();
                        return null;
                    case "encodeSymbols": return args[0];
                    case "getAvatarCount": case "getSleevesCount": return 1;
                    case "getScreenScale": return 1f;
                    case "showBugReportDialog": throw new IllegalStateException(java.util.Arrays.toString(args));
                }
                if (method.isDefault()) return InvocationHandler.invokeDefault(proxy, method, args);
                if (method.getReturnType() == void.class) return null;
                if (method.getReturnType() == boolean.class) return false;
                throw new UnsupportedOperationException("IGuiBase." + name);
            }));
        FModel.initialize(null, prefs -> {
            prefs.setPref(FPref.UI_LANGUAGE, "en-US");
            prefs.setPref(FPref.DECKGEN_CARDBASED, false);
            prefs.setPref(FPref.LOAD_CARD_SCRIPTS_LAZILY, lazyCards);
            prefs.setPref(FPref.YIELD_AUTO_PASS_NO_ACTIONS, false);
            return null;
        });
    }
}
