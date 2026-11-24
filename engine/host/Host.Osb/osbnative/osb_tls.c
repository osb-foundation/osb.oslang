#include <stdint.h>

/* Thread-local storage for our custom allocator */
static __thread uint64_t tls_alloc = 0;
static __thread uint64_t tls_limit = 0;
static __thread uint64_t tls_tp = 0;

uint64_t osb_tls_tp(void) {
    return tls_tp;
}

uint64_t osb_tls_get_alloc(void) {
    return tls_alloc;
}

uint64_t osb_tls_get_limit(void) {
    return tls_limit;
}

void osb_tls_set_alloc(uint64_t v) {
    tls_alloc = v;
}

void osb_tls_set_limit(uint64_t v) {
    tls_limit = v;
}
