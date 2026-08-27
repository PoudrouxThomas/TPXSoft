import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService, LoginRequest, RegisterRequest, User } from '@tpxsoft/auth-client';
import { ACCESS_TOKEN_KEY, REFRESH_TOKEN_KEY } from './auth-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly api = inject(ApiService);

    private readonly currentUserSignal = signal<User | null>(null);
    readonly currentUser = this.currentUserSignal.asReadonly();
    readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);

    private readonly readyPromise: Promise<void>;

    constructor() {
        const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
        this.readyPromise = accessToken ? this.rehydrate() : Promise.resolve();
    }

    /** Resolves once construction-time rehydration (if any) has settled — routes guarded by
     * `authGuard` await this so a reload with a valid stored token doesn't get bounced to
     * `/login` while `/auth/me` is still in flight. */
    ready(): Promise<void> {
        return this.readyPromise;
    }

    private async rehydrate(): Promise<void> {
        try {
            const user = await this.api.getCurrentUser();
            this.currentUserSignal.set(user);
        } catch {
            this.clearStorage();
            this.currentUserSignal.set(null);
        }
    }

    async register(req: RegisterRequest): Promise<void> {
        const tokens = await this.api.register({ body: req });
        this.storeTokens(tokens.accessToken, tokens.refreshToken);
        const user = await this.api.getCurrentUser();
        this.currentUserSignal.set(user);
    }

    async login(req: LoginRequest): Promise<void> {
        const tokens = await this.api.login({ body: req });
        this.storeTokens(tokens.accessToken, tokens.refreshToken);
        const user = await this.api.getCurrentUser();
        this.currentUserSignal.set(user);
    }

    async logout(): Promise<void> {
        const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
        if (refreshToken) {
            try {
                await this.api.logout({ body: { refreshToken } });
            } catch {
                // best-effort - still clear local state below
            }
        }
        this.clearStorage();
        this.currentUserSignal.set(null);
    }

    private storeTokens(accessToken: string, refreshToken: string): void {
        localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    }

    private clearStorage(): void {
        localStorage.removeItem(ACCESS_TOKEN_KEY);
        localStorage.removeItem(REFRESH_TOKEN_KEY);
    }
}
