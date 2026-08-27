import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
    function setup(isAuthenticated: boolean) {
        const authServiceStub = {
            isAuthenticated: () => isAuthenticated,
            ready: () => Promise.resolve(),
        };
        const urlTree = {} as UrlTree;
        const routerStub = { createUrlTree: vi.fn().mockReturnValue(urlTree) };

        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useValue: authServiceStub },
                { provide: Router, useValue: routerStub },
            ],
        });

        return { routerStub, urlTree };
    }

    function runGuard() {
        return TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/' } as never));
    }

    it('allows activation when authenticated', async () => {
        setup(true);
        const result = await runGuard();
        expect(result).toBe(true);
    });

    it('redirects to /login when not authenticated', async () => {
        const { routerStub, urlTree } = setup(false);
        const result = await runGuard();
        expect(routerStub.createUrlTree).toHaveBeenCalledWith(['/login']);
        expect(result).toBe(urlTree);
    });

    it('waits for rehydration to settle before deciding (reload with a stored token)', async () => {
        let resolveReady!: () => void;
        const authServiceStub = {
            isAuthenticated: () => true,
            ready: () => new Promise<void>((resolve) => (resolveReady = resolve)),
        };
        const urlTree = {} as UrlTree;
        const routerStub = { createUrlTree: vi.fn().mockReturnValue(urlTree) };

        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useValue: authServiceStub },
                { provide: Router, useValue: routerStub },
            ],
        });

        const resultPromise = runGuard();
        resolveReady();
        const result = await resultPromise;

        expect(result).toBe(true);
    });
});
