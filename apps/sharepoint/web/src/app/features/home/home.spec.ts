import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { User } from '@tpxsoft/auth-client';
import { Home } from './home';
import { AuthService } from '../../core/auth/auth.service';

describe('Home', () => {
    const testUser: User = {
        id: 'user-1',
        email: 'jane@example.com',
        orgId: 'org-1',
        orgName: 'Acme',
        role: 'Admin',
    };

    let authServiceStub: {
        currentUser: ReturnType<typeof signal<User | null>>;
        logout: ReturnType<typeof vi.fn>;
    };
    let router: Router;

    beforeEach(async () => {
        authServiceStub = { currentUser: signal(testUser), logout: vi.fn() };

        await TestBed.configureTestingModule({
            imports: [Home],
            providers: [provideRouter([]), { provide: AuthService, useValue: authServiceStub }],
        }).compileComponents();

        router = TestBed.inject(Router);
        vi.spyOn(router, 'navigate').mockResolvedValue(true);
    });

    it('shows the org name, not the org id', () => {
        const fixture = TestBed.createComponent(Home);
        fixture.detectChanges();

        const text = fixture.nativeElement.textContent as string;
        expect(text).toContain('Acme');
        expect(text).not.toContain('org-1');
    });

    it('logout calls AuthService.logout and navigates to /login', async () => {
        authServiceStub.logout.mockResolvedValue(undefined);
        const fixture = TestBed.createComponent(Home);
        const component = fixture.componentInstance;

        await component.onLogout();

        expect(authServiceStub.logout).toHaveBeenCalled();
        expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
});
