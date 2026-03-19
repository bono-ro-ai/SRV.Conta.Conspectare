import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from "react";
import type { ReactNode } from "react";
import {
  getStoredApiKey,
  setStoredApiKey,
  clearStoredApiKey,
} from "../services/api/authFetch";

interface AuthContextValue {
  isAuthenticated: boolean;
  apiKey: string | null;
  login: (apiKey: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [apiKey, setApiKey] = useState<string | null>(() => getStoredApiKey());

  const login = useCallback((key: string) => {
    setStoredApiKey(key);
    setApiKey(key);
  }, []);

  const logout = useCallback(() => {
    clearStoredApiKey();
    setApiKey(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated: apiKey !== null,
      apiKey,
      login,
      logout,
    }),
    [apiKey, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
