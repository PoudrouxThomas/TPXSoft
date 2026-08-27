import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideApiConfiguration } from '@tpxsoft/auth-client/api-configuration';
import { User } from '@tpxsoft/auth-client';
import { AuthService } from './auth.service';
import { ACCESS_TOKEN_KEY, REFRESH_TOKEN_KEY } from './auth-storage';

const ROOT_URL = 'http://localhost:5080';

const testUser: User = {
    id: 'user-1',
    email: 'jane@example.com',
    orgId: 'org-1',
    role: 'Admin',
};

/**
 * The generated client bridges Observables to Promises (`firstValueFrom`), and
 * `AuthService` chains further `await`s on top of that. Each of those hops needs its
 * own microtask tick to resolve, so tests wait on a macrotask boundary (which always
 * runs after any number of pending microtasks) between "flush a mocked response" and
 * "expect the next request to have gone out".
 */
function flushPromises(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('AuthService', () => {
    let httpMock: HttpTestingController;

    beforeEach(() => {
        localStorage.clear();
    });

    function setup(): AuthService {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                provideApiConfiguration(ROOT_URL),
            ],
        });
        httpMock = TestBed.inject(HttpTestingController);
        return TestBed.inject(AuthService);
    }

    afterEach(() => {
        httpMock.verify();
    });

    it('register: on success populates currentUser and stores tokens', async () => {
        const service = setup();

        const registerPromise = service.register({
            email: 'jane@example.com',
            password: 'password123',
            orgName: 'Acme',
        });

        await flushPromises();
        const registerReq = httpMock.expectOne(`${ROOT_URL}/auth/register`);
        expect(registerReq.request.method).toBe('POST');
        registerReq.flush({ accessToken: 'access-1', refreshToken: 'refresh-1' });

        await flushPromises();
        const meReq = httpMock.expectOne(`${ROOT_URL}/auth/me`);
        meReq.flush(testUser);

        await registerPromise;

        expect(service.currentUser()).toEqual(testUser);
        expect(service.isAuthenticated()).toBe(true);
        expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBe('access-1');
        expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    });

    it('login: on success populates currentUser and stores tokens', async () => {
        const service = setup();

        const loginPromise = service.login({
            email: 'jane@example.com',
            password: 'password123',
        });

        await flushPromises();
        const loginReq = httpMock.expectOne(`${ROOT_URL}/auth/login`);
        expect(loginReq.request.method).toBe('POST');
        loginReq.flush({ accessToken: 'access-2', refreshToken: 'refresh-2' });

        await flushPromises();
        const meReq = httpMock.expectOne(`${ROOT_URL}/auth/me`);
        meReq.flush(testUser);

        await loginPromise;

        expect(service.currentUser()).toEqual(testUser);
        expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBe('access-2');
    });

    it('login: on failure does not set currentUser and rejects', async () => {
        const service = setup();

        const loginPromise = service.login({
            email: 'jane@example.com',
            password: 'wrong-password',
        });
        // prevent an unhandled-rejection warning while we assert on the pending request below
        loginPromise.catch(() => {});

        await flushPromises();
        const loginReq = httpMock.expectOne(`${ROOT_URL}/auth/login`);
        loginReq.flush(
            { message: 'Invalid email or password.' },
            { status: 401, statusText: 'Unauthorized' },
        );

        await expect(loginPromise).rejects.toBeTruthy();
        expect(service.currentUser()).toBeNull();
        expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();
    });

    it('logout: clears currentUser and storage', async () => {
        const service = setup();

        const loginPromise = service.login({
            email: 'jane@example.com',
            password: 'password123',
        });
        await flushPromises();
        httpMock.expectOne(`${ROOT_URL}/auth/login`).flush({
            accessToken: 'access-3',
            refreshToken: 'refresh-3',
        });
        await flushPromises();
        httpMock.expectOne(`${ROOT_URL}/auth/me`).flush(testUser);
        await loginPromise;

        const logoutPromise = service.logout();
        await flushPromises();
        const logoutReq = httpMock.expectOne(`${ROOT_URL}/auth/logout`);
        expect(logoutReq.request.body).toEqual({ refreshToken: 'refresh-3' });
        logoutReq.flush(null);
        await logoutPromise;

        expect(service.currentUser()).toBeNull();
        expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();
        expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBeNull();
    });

    it('rehydrates currentUser from a stored access token on construction', async () => {
        localStorage.setItem(ACCESS_TOKEN_KEY, 'existing-access-token');
        localStorage.setItem(REFRESH_TOKEN_KEY, 'existing-refresh-token');

        const service = setup();

        await flushPromises();
        const meReq = httpMock.expectOne(`${ROOT_URL}/auth/me`);
        meReq.flush(testUser);

        // allow the fire-and-forget rehydrate() promise chain to settle
        await flushPromises();

        expect(service.currentUser()).toEqual(testUser);
    });
});
