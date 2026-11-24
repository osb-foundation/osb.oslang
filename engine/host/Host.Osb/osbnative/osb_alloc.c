#include <stdint.h>
#include <string.h>

/* Forward declarations for TLS */
extern uint64_t osb_tls_get_alloc(void);
extern uint64_t osb_tls_get_limit(void);
extern void osb_tls_set_alloc(uint64_t v);

/* 
 * Fast object allocation - simple bump pointer from thread-local arena
 * Returns pointer to allocated object (with EEType pointer at offset 0)
 */
void* RhpNewFast(uint64_t size) {
    uint64_t alloc = osb_tls_get_alloc();
    uint64_t limit = osb_tls_get_limit();
    
    // Align to 8 bytes
    uint64_t aligned_size = (size + 7) & ~7ULL;
    
    if (alloc + aligned_size > limit) {
        // Out of memory - for now return NULL
        // In a real implementation, this would trigger GC or expand arena
        return NULL;
    }
    
    void* obj = (void*)alloc;
    osb_tls_set_alloc(alloc + aligned_size);
    return obj;
}

/*
 * String allocation - creates a managed string object
 * Layout: EEType* (8 bytes) + length (8 bytes) + chars (2 bytes each, null-terminated)
 */
void* RhNewString(uint64_t length) {
    // String object: EEType + length + char buffer
    uint64_t size = 16 + (length + 1) * 2; // 16 for header, 2 bytes per char (UTF-16)
    void* obj = RhpNewFast(size);
    if (!obj) return NULL;
    
    // Zero initialize
    memset(obj, 0, size);
    
    // Set length (at offset 8)
    *(uint64_t*)((uint8_t*)obj + 8) = length;
    
    return obj;
}

/*
 * Write barrier - no-op for our no-GC runtime
 */
void RhpAssignRef(void** dst, void* src) {
    *dst = src;
}

/*
 * Array allocation - simple bump pointer
 */
void* RhpNewArrayFast(uint64_t elementSize, uint64_t length) {
    // Array layout: EEType* + length + elements
    uint64_t size = 16 + elementSize * length;
    void* obj = RhpNewFast(size);
    if (!obj) return NULL;
    
    memset(obj, 0, size);
    *(uint64_t*)((uint8_t*)obj + 8) = length;
    return obj;
}

/*
 * Pointer array allocation
 */
void* RhpNewPtrArrayFast(uint64_t length) {
    return RhpNewArrayFast(8, length); // 8 bytes per pointer
}

/*
 * Exception throwing - for now just abort
 */
__attribute__((noreturn)) void RhpThrowEx(void* exception) {
    // In a real implementation, this would unwind the stack
    // For now, just halt
    asm volatile("hlt");
    while(1) {}
}

/*
 * Checked write barrier - same as regular for no-GC
 */
void RhpCheckedAssignRef(void** dst, void* src) {
    *dst = src;
}

/*
 * Byref write barrier - same as regular for no-GC
 */
void RhpByRefAssignRef(void** dst, void* src) {
    *dst = src;
}

/*
 * Initial dynamic interface dispatch - stub
 */
void* RhpInitialDynamicInterfaceDispatch(void* obj, void* interfaceType) {
    // For now return NULL - would need proper interface dispatch
    return NULL;
}
