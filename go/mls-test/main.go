package main

import (
	"flag"
	"fmt"
	"log"
	"strconv"
	"time"

	slim "github.com/agntcy/slim-bindings-go"
)

const (
	defaultServer = "http://localhost:46357"
	defaultSecret = "demo-shared-secret-min-32-chars!!"
)

func oddEven(payload []byte) string {
	n, err := strconv.ParseInt(string(payload), 10, 64)
	if err != nil {
		return "not a number"
	}
	if n%2 == 0 {
		return "even"
	}
	return "odd"
}

func main() {
	server := flag.String("server", defaultServer, "SLIM server endpoint")
	sharedSecret := flag.String("shared-secret", defaultSecret, "Shared secret (min 32 chars)")
	enableMls := flag.Bool("enable-mls", false, "Enable MLS encryption")

	flag.Parse()

	fmt.Println("=== SLIM MLS Test: Bob (Go Receiver) — Odd/Even ===")
	fmt.Println()

	slim.InitializeWithDefaults()

	localName := slim.NewName("org", "bob", "v1")

	app, err := slim.GetGlobalService().CreateAppWithSecret(localName, *sharedSecret)
	if err != nil {
		log.Fatalf("Failed to create app: %v", err)
	}
	defer app.Destroy()

	config := slim.NewInsecureClientConfig(*server)
	connID, err := slim.GetGlobalService().ConnectAsync(config)
	if err != nil {
		log.Fatalf("Failed to connect: %v", err)
	}

	if err := app.SubscribeAsync(app.Name(), &connID); err != nil {
		log.Fatalf("Failed to subscribe: %v", err)
	}

	fmt.Printf("  Identity : org/bob/v1\n")
	fmt.Printf("  Server   : %s\n", *server)
	fmt.Printf("  Conn ID  : %d\n", connID)
	fmt.Printf("  MLS      : %v\n", map[bool]string{true: "ENABLED", false: "disabled"}[*enableMls])
	fmt.Println()
	fmt.Println("Waiting for incoming sessions from Alice...")
	fmt.Println()

	for {
		session, err := app.ListenForSessionAsync(nil)
		if err != nil {
			continue
		}

		fmt.Println("[session] New session established!")
		go handleSession(app, session)
	}
}

func handleSession(app *slim.App, session *slim.Session) {
	defer app.DeleteSessionAndWaitAsync(session)

	for {
		timeout := 60 * time.Second
		msg, err := session.GetMessageAsync(&timeout)
		if err != nil {
			fmt.Printf("[session] Ended: %v\n", err)
			return
		}

		text := string(msg.Payload)
		fmt.Printf("  << Received: %s\n", text)

		reply := oddEven(msg.Payload)
		if err := session.PublishToAndWaitAsync(msg.Context, []byte(reply), nil, nil); err != nil {
			fmt.Printf("[session] Error replying: %v\n", err)
			return
		}

		fmt.Printf("  >> Replied : %s\n", reply)
	}
}
