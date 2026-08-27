import { HttpInterceptorFn } from '@angular/common/http';
import { ACCESS_TOKEN_KEY } from './auth-storage';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
    if (!accessToken) {
        return next(req);
    }

    return next(
        req.clone({
            setHeaders: { Authorization: `Bearer ${accessToken}` },
        }),
    );
};
