workspace "Limited Underground Firmware Loader" "Inspection-only source at d74832b; product development frozen." {
    !impliedRelationships false
    model {
        operator = person "Operator" "Explicitly selects a family and a local candidate."
        files = softwareSystem "Windows file system" "External untrusted candidate storage." {
            tags "External"
        }
        authorities = softwareSystem "Target project authorities" "OpenTrail and OpenGauge own target, artifact, compatibility and recovery facts." {
            tags "External"
        }
        loader = softwareSystem "Limited Underground Firmware Loader — Preview" "Production inspection only; no device or installation authority." {
            desktop = container "Windows desktop application" "One WPF process; exact session and bounded offline inspection." "C# / .NET 10 / WPF" {
                ui = component "WPF composition and offline workflow" "Explicit file choice and sanitized outcomes; no production engine factory." "WPF / C#"
                session = component "Session and provider lifecycle" "Exact context, lease and generation; Trail provider and empty signer trust." "C#"
                inspector = component "Candidate inspector" "Bounded ZIP, CRC, canonical manifest and image digest; no admission." "C#"
                host = component "Synthetic host pipeline" "Fixture admission, preservation preflight, finite engine and uncertainty store; tests only." "C#" {
                    tags "HostOnly"
                }
                ui -> session "Selects family and checks current authority"
                ui -> inspector "Inspects explicitly opened stream"
                ui -> host "Test injection only; no production composition" {
                    tags "HostOnly"
                }
            }
        }
        operator -> loader "Selects family and requests inspection"
        loader -> files "Reads explicitly selected candidate"
        authorities -> loader "Source-pinned target key and hash provenance"
        operator -> desktop "Uses local window"
        desktop -> files "Read-only local stream"
        authorities -> desktop "Embedded target constants; no fetch or network"
        operator -> ui "Uses explicit chooser"
        ui -> files "Opens one selected file read-only"
    }
    views {
        systemContext loader "LoaderContext" "Operator, input and target-owner boundaries; no device commands." {
            include *
            autoLayout lr
        }
        container loader "LoaderContainers" "One desktop runtime; classes are not separate containers." {
            include *
            autoLayout lr
        }
        component desktop "LoaderComponents" "Production inspection versus host-only test pipeline." {
            include *
            autoLayout lr
        }
        styles {
            element "Element" {
                color #ffffff
            }
            element "Person" {
                shape Person
                background #174a72
            }
            element "Software System" {
                background #174a72
            }
            element "Container" {
                background #276a9b
            }
            element "Component" {
                background #276a9b
            }
            element "External" {
                background #616b75
            }
            element "HostOnly" {
                background #a46512
            }
            relationship "HostOnly" {
                color #a46512
                dashed true
            }
        }
    }
}
