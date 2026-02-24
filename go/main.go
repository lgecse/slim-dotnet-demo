package main

import (
	"flag"
	"fmt"
	"log"
	"math/rand"
	"strconv"
	"time"

	slim "github.com/agntcy/slim-bindings-go"
)

const (
	defaultServer   = "http://localhost:46357"
	defaultSecret   = "demo-shared-secret-min-32-chars!!"
	defaultMinNum   = 1
	defaultMaxNum   = 100
	defaultIterations = 10
)

func main() {
	remote := flag.String("remote", "org/alice/v1", "Remote ID (org/namespace/app)")
	server := flag.String("server", defaultServer, "SLIM server endpoint")
	iterations := flag.Int("iterations", defaultIterations, "Number of numbers to send")
	minNum := flag.Int("min", defaultMinNum, "Minimum random number")
	maxNum := flag.Int("max", defaultMaxNum, "Maximum random number")
	sharedSecret := flag.String("shared-secret", defaultSecret, "Shared secret (min 32 chars)")
	noMls := flag.Bool("no-mls", false, "Disable MLS encryption (enabled by default)")

	flag.Parse()

	enableMls := !*noMls

	fmt.Println("=== SLIM Demo: Bob (Go Sender) — Odd/Even ===")
	fmt.Println()

	slim.InitializeWithDefaults()

	localName, err := slim.NameFromString("org/bob/v1")
	if err != nil {
		log.Fatalf("Failed to parse local name: %v", err)
	}

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
	fmt.Printf("  Remote   : %s\n", *remote)
	fmt.Printf("  Conn ID  : %d\n", connID)
	fmt.Printf("  Range    : %d–%d\n", *minNum, *maxNum)
	fmt.Printf("  MLS      : %s\n", map[bool]string{true: "ENABLED", false: "disabled"}[enableMls])
	fmt.Println()

	remoteName, err := slim.NameFromString(*remote)
	if err != nil {
		log.Fatalf("Failed to parse remote name: %v", err)
	}

	if err := app.SetRouteAsync(remoteName, connID); err != nil {
		log.Fatalf("Failed to set route: %v", err)
	}
	fmt.Printf("Route set to %s\n", *remote)

	sessConfig := slim.SessionConfig{
		SessionType: slim.SessionTypePointToPoint,
		EnableMls:   enableMls,
	}

	fmt.Printf("Creating session to %s...\n", *remote)
	session, err := app.CreateSessionAndWaitAsync(sessConfig, remoteName)
	if err != nil {
		log.Fatalf("Failed to create session: %v", err)
	}
	defer app.DeleteSessionAndWaitAsync(session)

	time.Sleep(100 * time.Millisecond)

	fmt.Println("Ready!")
	fmt.Println()

	for i := 0; i < *iterations; i++ {
		n := *minNum + rand.Intn(*maxNum-*minNum+1)
		msg := strconv.Itoa(n)

		if err := session.PublishAndWaitAsync([]byte(msg), nil, nil); err != nil {
			fmt.Printf("  !! Error sending message %d/%d: %v\n", i+1, *iterations, err)
			continue
		}

		fmt.Printf("  >> Sent    : %d (%d/%d)\n", n, i+1, *iterations)

		timeout := 5 * time.Second
		reply, err := session.GetMessageAsync(&timeout)
		if err != nil {
			fmt.Printf("  !! No reply for message %d/%d: %v\n", i+1, *iterations, err)
			continue
		}

		fmt.Printf("  << Received: %s (%d/%d)\n", string(reply.Payload), i+1, *iterations)
		time.Sleep(1 * time.Second)
	}

	fmt.Println()
	fmt.Println("Done.")
}
