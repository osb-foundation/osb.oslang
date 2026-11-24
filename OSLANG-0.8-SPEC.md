# **OSLANG 0.8 Specification**
**Base:** OSLANG 0.73  
**Version:** 0.8  
**Type:** Breaking Change  
**Primary goal:** Formalize language-level object identity, equality, closure semantics, and object lifetime.  
# **1. Overview**

OSLANG 0.8 formalizes the language-level memory semantics approved for the 0.8 release: object identity, equality by value category, closure equality, closure capture semantics, object lifetime, and allocation failure.

This version contains a **breaking change** in closure equality: closures previously compared as always unequal (including a closure compared with itself) and now compare by identity, so a closure is equal to itself while distinct closures remain unequal.

Apart from that intentional change, 0.8 preserves the semantics of OSLANG 0.73. No syntax, operators, or standard-library additions are introduced in 0.8.
# **2. Object identity**

Managed objects (arrays, class instances, closures, and enum sets) have logical identity independent of their physical memory representation.

The language specification defines observable behavior only. It must not prescribe:

- native pointers;
- object offsets;
- handle representation;
- index-plus-generation implementation;
- heap layout;
- allocator strategy;
- garbage collector implementation.

The implementation architecture has separately selected index-plus-generation handles, but that is not a language-level requirement, and no program may depend on it.
# **3. Equality semantics**

Identity equality and value equality are separate semantic concepts. Equality is determined by value category:

| Value category | Equality |
|---|---|
| `NULL` | Equal only to `NULL` |
| Boolean | Value equality |
| Number | Value equality |
| String | Content equality |
| Enum | Enum type and member equality |
| Array | Object identity |
| Class instance | Object identity |
| EnumSet | Object identity |
| Closure | Object identity |
| KeyEvent | Preserve characterized special-value behavior |

Physical heap allocation does not automatically confer language-level object identity. A value stored on the heap in the implementation is not, by that fact alone, a managed object with identity semantics in the language.
# **4. Closure equality — intentional breaking change**

Closure equality changes in OSLANG 0.8.

**Previous behavior:**

- Closures always compared unequal.
- Even a closure compared with itself was considered unequal (non-reflexive).

**New behavior (OSLANG 0.8):**

- A closure is equal to itself (reflexive).
- Distinct closures are not equal.
- Two closures with identical function bodies and captured values remain distinct if they are distinct closure instances.
- Closure equality is identity-based and reflexive.

**Compatibility impact:** Programs that relied on the previous non-reflexive closure behavior — for example, code that branches on a closure comparing unequal to itself, or on two closures with identical bodies comparing unequal — may need adjustment. This is an intentional compatibility change warranting the 0.8 minor version. Current implementations do not yet implement the new behavior; implementation work is required to bring the runtimes into compliance.
# **5. Closure capture semantics**

Closure capture semantics are preserved as characterized:

- Captured primitive values are copied into the closure environment.
- Captured object references preserve identity and alias the same object.
- Multiple closures share an environment only when given the same capture array.
- Closures created in the same lexical scope must not be assumed to share a mutable environment.
- Captured values remain usable after the original scope exits.
- Recursive function declarations remain supported.

Do not generalize beyond the available characterization evidence.
# **6. KeyEvent**

KeyEvent remains a special opaque value.

It is not automatically a managed language object merely because the current implementation uses a heap reference. Its characterized equality behavior is preserved.

KeyEvent is not included in the initial managed object semantic category without authoritative language evidence.
# **7. Object lifetime**

Objects referenced by reachable language values must remain valid. Unreachable objects may be reclaimed at implementation-defined times. The language does not promise deterministic reclamation.

No finalizers, destructors, weak references, or explicit disposal semantics are introduced.

The specification does not prescribe garbage collection, reference counting, arenas, or any other physical memory strategy.
# **8. Allocation failure**

Allocation-failure semantics remain unresolved and deferred.

The 0.8 specification must not:

- promise catchable out-of-memory behavior;
- introduce new `CATCH` semantics;
- promise allocation recovery;
- define a universal failure mechanism unsupported by the current runtime architecture.

Allocation failure is identified as deferred and unresolved. Current implementation limitations are not turned into permanent language guarantees.
# **9. Breaking changes**

OSLANG 0.8 is explicitly a **breaking release**. The normative change is:

## **9.1 Closure equality**

Closure equality changes from always-false behavior to identity equality.

- Comparing a closure with itself now produces equality (was always false).
- Distinct closure instances remain unequal, regardless of equivalent code or captured values.
- Programs relying on the previous non-reflexive closure behavior may need adjustment.

## **9.2 Categories of change**

- **Changes in normative language semantics:** closure equality becomes identity-based and reflexive (section 4).
- **Existing behavior retained from previous versions:** all equality semantics for non-closure categories (section 3), closure capture semantics (section 5), KeyEvent classification (section 6), object lifetime (section 7), and allocation-failure deferral (section 8).
- **Implementation work required to bring the runtimes into compliance:** implementing identity-based closure equality, including reflexive self-comparison; current implementations do not yet implement this behavior.

No other breaking changes are introduced in 0.8.
# **10. Compatibility and migration**

For applications being migrated from 0.73:

- Equality of `NULL`, booleans, numbers, strings, enums, arrays, class instances, and enum sets is unchanged.
- A closure compared with itself now returns TRUE where it previously returned FALSE.
- Two distinct closures with identical bodies and captures still compare FALSE.
- Code that branches on closures being always unequal should be reviewed before adopting 0.8.
- All other language features, syntax, and standard-library behavior from 0.73 continue to apply unchanged.
