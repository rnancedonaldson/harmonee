# Harmonee Architecture Overview

## Purpose
The architecture of the Harmonee project is intended to create a responsive, scaleable, testable application following Clean Architecture best practices. The project has started as an Aspire focused microservice based mono repo for MVP purposes, but is designed in such a way that services may be broken off into separate repositories if needed.



## Architectural Layout
- src
    - Harmonee.Domain: models, enums, exceptions, anything that is used by most services
    - Harmonee.Application: defines the core business logic and interfaces for communicating with different services
    - Harmonee.Infrastructure: handles all dependencies; interacting with databases, third party services, etc.
    - Harmonee.Presentation: Shared resources for web and mobile app UI applications
    - Harmonee.ServiceDefaults: Shared logic to tie in services to Aspire 
    - Harmonee.Services: the actual applications that provide services (APIs, UI, etc.)
- tests
    - Harmonee.Domain.Tests: tests core models and their methods
    - Harmonee.Application.Tests: tests business logic and use cases, services
    - Harmonee.Presentation.Tests: tests shared presentation resources and presentation applications
- docs
    - Explanations of services, APIs, use cases, OpenAPI specs, etc.

## Architectural Rules
- General:
    - Each shared library will utilize a feature-based subfolder structure to maintain separation of concerns and ease of navigation.
        - If a subfolder contains more than 5 models of similar type as well as models of different types (i.e. 5 request objects, 2 DTOs), create another subfolder representing the group and move those models into it. Using the same example, create a Requests folder and leave the DTOs in place.
        - If a subfolder contains more than 10 models of any type, move the models into folders.
    - All code reviews are to enforce strict adherence to these guidelines.
- Domain:
    - The domain layer describes the *definitions* of business models, not the logic.
    - Domain is the single source of truth for the entire application; it has no dependencies to other projects, but is a dependency for most/all other projects.
    - May also contain base or shared models that facilitate abstractions or implementations- again, no business logic!
- Application:
    - Models the shared application structure for the project by defining the interfaces services and repositories will use, the concrete implementations of those services (not repositories, see Infrastructure)
    - Use CQRS (Command Query Responsibility Segregation) pattern to encapsulate processes. Commands will always alter data, queries will only ever retrieve data.
- Infrastructure:
- Web/Presentation:
- ServiceDefaults: